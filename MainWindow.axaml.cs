using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ChatProject.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using BCryptNet = BCrypt.Net.BCrypt;

namespace ChatProject;

public partial class MainWindow : Window
{
    private readonly ChatClient _client = new();
    private string? _activeChat;
    private int _activeGroupId;
    private bool _activeChatIsGroup;
    private ChatViewMode _viewMode = ChatViewMode.Contacts;
    private int _currentUserId;
    private string _username = "";
    private int? _editingContactId;
    private readonly HashSet<int> _blockedContactIds = new();

    private enum ChatViewMode
    {
        Contacts,
        BlackList,
        Groups
    }

    public MainWindow()
    {
        InitializeComponent();
        _client.MessageReceived += OnMessageReceived;
        _client.GroupMessageReceived += OnGroupMessageReceived;

        ContactsBorder.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == "IsVisible")
                Console.WriteLine($"[diag] ContactsBorder.IsVisible -> {ContactsBorder.IsVisible} (now={DateTime.Now:HH:mm:ss.fff})");
        };
        Closed += (_, _) => Console.WriteLine("[diag] WINDOW CLOSED");
    }

    private static (string Host, int Port) GetServerEndpoint()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        return (
            configuration["Server:Host"] ?? "127.0.0.1",
            int.Parse(configuration["Server:Port"] ?? "5000"));
    }

    private void SwitchForms()
    {
        SignInNavigationButton.IsEnabled = !SignInNavigationButton.IsEnabled;
        EmailTextBox.IsVisible = !EmailTextBox.IsVisible;
        PasswordTextBox.IsVisible = !PasswordTextBox.IsVisible;
        SignInButton.IsVisible = !SignInButton.IsVisible;

        UsernameTextBox.IsVisible = !UsernameTextBox.IsVisible;
        SignUpNavigationButton.IsEnabled = !SignUpNavigationButton.IsEnabled;
        EmailTextBoxSignUp.IsVisible = !EmailTextBoxSignUp.IsVisible;
        PasswordTextBoxSignUp.IsVisible = !PasswordTextBoxSignUp.IsVisible;
        SignUnButton.IsVisible = !SignUnButton.IsVisible;
    }

    private void SetViewMode(ChatViewMode mode)
    {
        _viewMode = mode;

        ContactsNavigationButton.IsEnabled = mode != ChatViewMode.Contacts;
        BlackListNavigationButton.IsEnabled = mode != ChatViewMode.BlackList;
        GroupsNavigationButton.IsEnabled = mode != ChatViewMode.Groups;

        ContactsModePanel.IsVisible = mode == ChatViewMode.Contacts;
        BlackListModePanel.IsVisible = mode == ChatViewMode.BlackList;
        GroupsModePanel.IsVisible = mode == ChatViewMode.Groups;
    }

    private void HideAuth()
    {
        TitleTextBlock.IsVisible = false;
        SignInNavigationButton.IsVisible = false;
        EmailTextBox.IsVisible = false;
        PasswordTextBox.IsVisible = false;
        SignInButton.IsVisible = false;

        UsernameTextBox.IsVisible = false;
        SignUpNavigationButton.IsVisible = false;
        EmailTextBoxSignUp.IsVisible = false;
        PasswordTextBoxSignUp.IsVisible = false;
        SignUnButton.IsVisible = false;

        ContactsBorder.IsVisible = true;
        BlackListNavigationButton.IsVisible = true;
        ContactsNavigationButton.IsVisible = true;
        GroupsNavigationButton.IsVisible = true;
        SetViewMode(ChatViewMode.Contacts);
    }

    private void SignUpNavigate_click(object? sender, RoutedEventArgs e)
    {
        SwitchForms();
    }

    private void SignInNavigate_click(object? sender, RoutedEventArgs e)
    {
        SwitchForms();
    }

    private void SignUpButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = SignUp();
    }

    private void SignInButton_Click(object? sender, RoutedEventArgs e)
    {
        _ = SignIn();
    }

    private async Task SignIn()
    {
        using var context = new AppContext();
        string email = EmailTextBox.Text ?? "";
        string password = PasswordTextBox.Text ?? "";
        if (email.Equals(string.Empty))
        {
            await ShowErrorAsync("Email can't be empty");
            return;
        }
        if (password.Equals(string.Empty))
        {
            await ShowErrorAsync("Password can't be empty");
            return;
        }

        User? user = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            await ShowErrorAsync("User not found");
            return;
        }
        bool isCorrectPassword = BCryptNet.Verify(password, user.Password);
        if (!isCorrectPassword)
        {
            await ShowErrorAsync("Password doesn't match");
            return;
        }
        
        HideAuth();
        await EnterChatAsync(user.Id, user.Username);
    }

    private async Task SignUp()
    {
        using var context = new AppContext();
        string username = UsernameTextBox.Text ?? "";
        string email = EmailTextBoxSignUp.Text ?? "";
        string password = PasswordTextBoxSignUp.Text ?? "";
        if (username.Equals(string.Empty))
        {
            await ShowErrorAsync("Username can't be empty");
            return;
        }
        if (email.Equals(string.Empty))
        {
            await ShowErrorAsync("Email can't be empty");
            return;
        }
        if (password.Equals(string.Empty))
        {
            await ShowErrorAsync("Password can't be empty");
            return;
        }

        User? findExistUser = await context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (findExistUser != null)
        {
            await ShowErrorAsync("User is already exist");
            return;
        }

        string hashedPassword = BCryptNet.HashPassword(password);
        var newUser = new User
        {
            Username = username,
            Email = email,
            Password = hashedPassword
        };
        await context.Users.AddAsync(newUser);
        await context.SaveChangesAsync();

        HideAuth();
        await EnterChatAsync(newUser.Id, username);
    }

    private async Task EnterChatAsync(int userId, string username)
    {
        _currentUserId = userId;
        _username = username;
        _activeChat = null;

        Console.WriteLine("[diag] EnterChatAsync: HideAuth()");
        await GetBlacklistedContacts();
        await GetAllContacts();
        await LoadGroupsAsync();
        Console.WriteLine("[diag] GetAllContacts done");

        var (host, port) = GetServerEndpoint();
        try
        {
            Console.WriteLine("[diag] connecting...");
            await _client.ConnectAsync(host, port, username);
            Console.WriteLine("[diag] connected OK");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[diag] connect FAILED: {ex.Message}");
            await ShowErrorAsync("Server is offline");
        }
    }

    private async Task GetAllContacts()
    {
        using var context = new AppContext();

        IList<Contact> contacts = await context.Contacts
            .Include(c => c.ContactUser)
            .Where(c => c.OwnerUserId == _currentUserId && !_blockedContactIds.Contains(c.Id))
            .ToListAsync();

        ContactsList.Children.Clear();
        foreach (var contact in contacts)
        {
            string name = string.IsNullOrEmpty(contact.DisplayName)
                ? contact.ContactUser?.Username ?? $"#{contact.ContactUserId}"
                : contact.DisplayName;

            ContactsList.Children.Add(CreateContactRow(name, contact.Id, false));
        }
    }

    private Panel CreateContactRow(string name, int contactId, bool blackList)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(1, GridUnitType.Auto)),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            Margin = new Avalonia.Thickness(0, 2)
        };

        var nameBtn = new Button
        {
            Content = name,
            Width = 100,
            Height = 30,
            Margin = new Avalonia.Thickness(0, 0, 4, 0)
        };
        nameBtn.Click += (_, _) =>
        {
            if (!blackList)
                SelectContact(name);
        };

        var editBtn = new Button
        {
            Content = "Edit",
            Width = 42,
            Height = 30,
            Margin = new Avalonia.Thickness(0, 0, 4, 0)
        };
        editBtn.Click += async (_, _) => await StartEditContactAsync(contactId, name);

        var deleteBtn = new Button
        {
            Content = "X",
            Width = 30,
            Height = 30
        };
        deleteBtn.Click += async (_, _) =>
        {
            if (blackList)
                await UnblockUserAsync(contactId);
            else
                await DeleteContactAsync(contactId, name);
        };

        Grid.SetColumn(nameBtn, 0);
        Grid.SetColumn(editBtn, 1);
        Grid.SetColumn(deleteBtn, 2);

        row.Children.Add(nameBtn);
        if (!blackList)
            row.Children.Add(editBtn);
        row.Children.Add(deleteBtn);
        return row;
    }

    private async void AddContactButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_editingContactId.HasValue)
        {
            await SaveContactEditAsync();
            return;
        }

        string target = NewContactTextBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(target))
        {
            await ShowErrorAsync("Username can't be empty");
            return;
        }

        using var context = new AppContext();
        var contactUser = await context.Users.FirstOrDefaultAsync(u => u.Username == target);
        if (contactUser is null || contactUser.Id == _currentUserId)
        {
            await ShowErrorAsync("User not found");
            return;
        }

        bool already = await context.Contacts.AnyAsync(c =>
            c.OwnerUserId == _currentUserId && c.ContactUserId == contactUser.Id);
        if (!already)
        {
            await context.Contacts.AddAsync(new Contact
            {
                OwnerUserId = _currentUserId,
                ContactUserId = contactUser.Id,
                DisplayName = contactUser.Username
            });
            await context.SaveChangesAsync();
        }

        NewContactTextBox.Clear();
        await GetAllContacts();
    }

    private async Task StartEditContactAsync(int contactId, string currentName)
    {
        _editingContactId = contactId;
        NewContactTextBox.Text = currentName;
        AddContactButton.Content = "save";
        await Task.CompletedTask;
    }

    private async Task SaveContactEditAsync()
    {
        if (_editingContactId == null) return;

        int contactId = _editingContactId.Value;
        string newName = NewContactTextBox.Text?.Trim() ?? "";

        using (var context = new AppContext())
        {
            Contact? contact = await context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
            if (contact == null)
            {
                await ShowErrorAsync("Contact not found");
                await ResetContactEditModeAsync();
                return;
            }

            if (string.IsNullOrEmpty(newName))
            {
                await ShowErrorAsync("Name can't be empty");
                return;
            }

            contact.DisplayName = newName;
            await context.SaveChangesAsync();
        }

        await ResetContactEditModeAsync();
        await GetAllContacts();
    }

    private async Task ResetContactEditModeAsync()
    {
        _editingContactId = null;
        AddContactButton.Content = "+ add contact";
        NewContactTextBox.Clear();
        await Task.CompletedTask;
    }

    private async Task DeleteContactAsync(int contactId, string contactName)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(
            "Delete contact",
            $"Delete '{contactName}'?",
            ButtonEnum.YesNo);
        var result = await box.ShowAsync();
        if (result != ButtonResult.Yes) return;

        using (var context = new AppContext())
        {
            Contact? contact = await context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
            if (contact != null)
            {
                context.Contacts.Remove(contact);
                await context.SaveChangesAsync();
            }
        }

        if (_activeChat == contactName)
        {
            _activeChat = null;
            ChatPanel.IsVisible = false;
        }

        await GetAllContacts();
    }

    private async Task LoadHistoryAsync(string chatName)
    {
        using var context = new AppContext();

        IList<Message> chatMessages;
        if (_activeChatIsGroup)
        {
            chatMessages = await GroupService.GetGroupHistoryAsync(_activeGroupId);
        }
        else
        {
            chatMessages = await context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.Sender.Username == _username && m.Receiver!.Username == chatName) ||
                    (m.Sender.Username == chatName && m.Receiver!.Username == _username))
                .OrderBy(m => m.SendAt)
                .ToListAsync();
        }

        MessagesList.Items.Clear();
        foreach (var message in chatMessages)
            MessagesList.Items.Add($"{message.Sender.Username}: {message.Text}");
    }

    private void SelectContact(string contactName)
    {
        _activeChat = contactName;
        _activeGroupId = 0;
        _activeChatIsGroup = false;
        ChatPanel.IsVisible = true;
        AddGroupMemberTextBox.IsVisible = false;
        AddGroupMemberButton.IsVisible = false;
        _ = LoadHistoryAsync(contactName);
    }

    private void SelectGroup(int groupId, string groupName)
    {
        _activeChat = groupName;
        _activeGroupId = groupId;
        _activeChatIsGroup = true;
        ChatPanel.IsVisible = true;
        AddGroupMemberTextBox.IsVisible = true;
        AddGroupMemberButton.IsVisible = true;
        _ = LoadHistoryAsync(groupName);
    }

    private async Task LoadGroupsAsync()
    {
        IList<Models.Group> groups = await GroupService.GetUserGroupsAsync(_currentUserId);

        GroupsList.Children.Clear();
        foreach (var group in groups)
        {
            var btn = new Button
            {
                Content = $"{group.Name} ({group.Members.Count})",
                Width = 160,
                Height = 30,
                Margin = new Avalonia.Thickness(0, 2)
            };
            btn.Click += (_, _) => SelectGroup(group.Id, group.Name);
            GroupsList.Children.Add(btn);
        }
    }

    private async void CreateGroupButton_Click(object? sender, RoutedEventArgs e)
    {
        string name = CreateGroupTextBox.Text?.Trim() ?? "";
        if (name.Equals(string.Empty))
        {
            await ShowErrorAsync("Group name can't be empty");
            return;
        }

        await GroupService.CreateGroupAsync(_username, name);
        CreateGroupTextBox.Clear();
        await LoadGroupsAsync();
    }

    private async void AddGroupMemberButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_activeChatIsGroup) return;

        string username = AddGroupMemberTextBox.Text?.Trim() ?? "";
        if (username.Equals(string.Empty))
        {
            await ShowErrorAsync("Username can't be empty");
            return;
        }

        await GroupService.AddMembersAsync(_activeGroupId, username);
        AddGroupMemberTextBox.Clear();
        await LoadGroupsAsync();
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        await SendMessageAsync();
    }

    private async void MessageInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
            await SendMessageAsync();
    }

    private async Task SendMessageAsync()
    {
        string text = MessageInput.Text ?? "";
        string to = _activeChat ?? "";
        if (text.Equals(string.Empty) || to.Equals(string.Empty)) return;

        if (_activeChatIsGroup)
        {
            await GroupService.SendMessageAsync(_client, _activeGroupId, _currentUserId, text);
            MessagesList.Items.Add($"{_username}: {text}");
            MessageInput.Clear();
            return;
        }

        using var context = new AppContext();
       
            var receiver = await context.Users.Include(u => u.BlacklistedContacts).ThenInclude(c => c.ContactUser).FirstOrDefaultAsync(u => u.Username == to);
            if (receiver == null)
            {
                await ShowErrorAsync("User not found");
                return;
            }
            var blockedCheck = receiver.BlacklistedContacts.FirstOrDefault(c => c.ContactUser.Username == _username);
            if (blockedCheck != null)
            {
                await ShowErrorAsync("you have been blocked");
                return;
            }
           
                context.Messages.Add(new Message
                {
                    SenderId = _currentUserId,
                    ReceiverId = receiver.Id,
                    Text = text,
                    SendAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
           
        
        
        await _client.SendMessageAsync(to, text);
        MessagesList.Items.Add($"{_username}: {text}");
        MessageInput.Clear();
    }

    private void OnMessageReceived(string from, string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (from == _activeChat)
                MessagesList.Items.Add($"{from}: {text}");
        });
    }

    private void OnGroupMessageReceived(string from, string groupName, string text)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_activeChatIsGroup && groupName == _activeChat)
                MessagesList.Items.Add($"{from}: {text}");
        });
    }

    private async Task ShowErrorAsync(string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard("Error", message, ButtonEnum.Ok);
        await box.ShowAsync();
    }

    private void BlackListNavigationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ChatViewMode.BlackList);
        _ = GetBlacklistedContacts();
    }

    private void ContactsNavigationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ChatViewMode.Contacts);
        _ = GetAllContacts();
    }

    private void GroupsNavigationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        SetViewMode(ChatViewMode.Groups);
        _ = LoadGroupsAsync();
    }

    private void AddContactBlackListButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if(NewBlackListContactContactTextBox.Text == null) return;
        
        _ = BlockUserAsync(NewBlackListContactContactTextBox.Text);
    }

    private async Task BlockUserAsync(string? username)
    {
        if (string.IsNullOrEmpty(username))
        {
            await ShowErrorAsync("Username can't be empty");
            return;
        }

        using var context = new AppContext();

        var userForBlock = await context.Users
            .FirstOrDefaultAsync(u => u.Username == username);

        if (userForBlock == null || userForBlock.Id == _currentUserId)
        {
            await ShowErrorAsync("User not found");
            return;
        }

        var currentUser = await context.Users
            .Include(u => u.BlacklistedContacts)
            .FirstOrDefaultAsync(u => u.Id == _currentUserId);

        var contactLink = await context.Contacts
            .FirstOrDefaultAsync(c => c.OwnerUserId == _currentUserId && c.ContactUserId == userForBlock.Id);

        if (contactLink == null)
        {
            contactLink = new Contact
            {
                OwnerUserId = _currentUserId,
                ContactUserId = userForBlock.Id,
                DisplayName = userForBlock.Username
            };
            context.Contacts.Add(contactLink);
        }

        if (currentUser != null && !currentUser.BlacklistedContacts.Any(c => c.Id == contactLink.Id))
        {
            currentUser.BlacklistedContacts.Add(contactLink);
        }

        await context.SaveChangesAsync();
        _blockedContactIds.Clear();
        await GetBlacklistedContacts();
        await GetAllContacts();
    }

    private async Task GetBlacklistedContacts()
    {
        using var context = new AppContext();

        var currentUser = await context.Users
            .Include(u => u.BlacklistedContacts)
            .ThenInclude(c => c.ContactUser)
            .FirstOrDefaultAsync(u => u.Id == _currentUserId);

        IList<Contact> blocked = currentUser?.BlacklistedContacts ?? new List<Contact>();
        _blockedContactIds.Clear();
        foreach (var contact in blocked)
            _blockedContactIds.Add(contact.Id);

        BlackListContactsList.Children.Clear();
        foreach (var contact in blocked)
        {
            string name = string.IsNullOrEmpty(contact.DisplayName)
                ? contact.ContactUser?.Username ?? $"#{contact.ContactUserId}"
                : contact.DisplayName;

            BlackListContactsList.Children.Add(CreateContactRow(name, contact.Id, true));
        }
    }

    private async Task UnblockUserAsync(int contactId)
    {
        using var context = new AppContext();

        var currentUser = await context.Users
            .Include(u => u.BlacklistedContacts)
            .FirstOrDefaultAsync(u => u.Id == _currentUserId);

        Contact? blocked = currentUser?.BlacklistedContacts
            .FirstOrDefault(c => c.Id == contactId);

        if (currentUser != null && blocked != null)
        {
            currentUser.BlacklistedContacts.Remove(blocked);
            await context.SaveChangesAsync();
        }

        await GetBlacklistedContacts();
    }
}
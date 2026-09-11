using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ChatProject.Models;
using Microsoft.EntityFrameworkCore;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using BCryptNet = BCrypt.Net.BCrypt;
namespace ChatProject;

public partial class MainWindow : Window
{
    private string? _error;
    
    public MainWindow()
    {
        InitializeComponent();
            
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
        var context = new AppContext();
        string email = EmailTextBox.Text ?? "";
        string password = PasswordTextBox.Text ?? "";
        if (email.Equals(string.Empty))
        {
            _error = "Email can't be empty";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;

        }
        if (password.Equals(string.Empty))
        {
            _error = "Password can't be empty";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;

        }

        User? user = await context.Users.FirstOrDefaultAsync((user => user.Email == email));
        if (user == null)
        {
            _error = "User not found";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;
        }
        bool isCorrectPassword = BCryptNet.Verify(password, user.Password);
        if (!isCorrectPassword)
        {
            _error = "Password doesn't match";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;
        }
        TcpClient  client = new TcpClient(IPAddress.Loopback.ToString(),5000);
        
        HideAuth();
    }

    private async Task SignUp()
    {
        var context = new AppContext();
        string username = UsernameTextBox.Text ?? "";
        string email = EmailTextBoxSignUp.Text ?? "";
        string password = PasswordTextBoxSignUp.Text ?? "";
        if (username.Equals(string.Empty))
        {
            _error = "Username can't be empty";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;

        }
        if (email.Equals(string.Empty))
        {
            _error = "Email can't be empty";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;

        }
        if (password.Equals(string.Empty))
        {
            _error = "Password can't be empty";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;

        }

        User? findExistUser = await context.Users.FirstOrDefaultAsync((user => user.Email == email));
        if (findExistUser != null)
        {
            _error = "User is already exist";
            var box = MessageBoxManager.GetMessageBoxStandard(
                "Error", 
                _error, 
                ButtonEnum.Ok);
            await box.ShowAsync();
            return;
        }

        string hashedPassword = BCryptNet.HashPassword(password);
        User? newUser = new User
        {
            Username = username,
            Email = email,
            Password = hashedPassword
        };
        await context.Users.AddAsync(newUser);
        await context.SaveChangesAsync();
        HideAuth();

    }
}
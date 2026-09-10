using System;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ChatProject;

public partial class MainWindow : Window
{
    
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

    private void SignUpNavigate_click(object? sender, RoutedEventArgs e)
    {
        SwitchForms();
    }

    private void SignInNavigate_click(object? sender, RoutedEventArgs e)
    {
        SwitchForms();
    }

    
}
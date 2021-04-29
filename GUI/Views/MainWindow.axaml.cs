using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ReactiveUI;
using System;
using System.Reactive;

namespace ChiaPlottStatusAvalonia.Views
{
    public class MainWindow : Window
    {
        public static MainWindow? Instance { get; private set; }
        public Func<string, bool> BtnClickWorkaround { get; set; }
        public Func<string, bool> TextChangeWorkaround { get; set; }

        public MainWindow()
        {
            Instance = this;
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }



        // FIXME: RemoveFolderCommand does not trigger. Why is button.Command null?
        public void RemoveFolderWorkaround(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            string folder = (string)button.CommandParameter;
            ReactiveCommand<string, Unit> command = (ReactiveCommand<string, Unit>)button.Command;
            if (command == null)
            {
                command = (ReactiveCommand<string, Unit>)button.Tag;
            }
            // button.Command.Execute(folder);
            BtnClickWorkaround.Invoke(folder);
        }


        // FIXME: apparently avalonia cannot tell me when a textbox text changes in my MainWindowViewModel
        public void OnKeyPressUp(object sender, KeyEventArgs e)
        {
            TextChangeWorkaround(((TextBox)sender).Text);
        }
    }
}

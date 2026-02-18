using Avalonia.Controls;

using SocialPublisher.ViewModels;

namespace SocialPublisher.Views;

public partial class MainWindow : Window {
    public MainWindow() {
        this.InitializeComponent();
        this.Loaded += (sender, args) => {
            if (this.DataContext is PublisherViewModel vm) {
                vm.TopLevelContext = TopLevel.GetTopLevel(this);
            }
        };
    }
}
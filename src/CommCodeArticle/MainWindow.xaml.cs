using System.Windows;
using CommCodeArticle.ViewModels;

namespace CommCodeArticle;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new CylinderArticleViewModel();
    }
}

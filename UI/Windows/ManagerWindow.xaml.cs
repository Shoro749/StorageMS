using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Windows;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for ManagerWindow.xaml
    /// </summary>
    public partial class ManagerWindow : Window
    {
        private readonly User _currentUser;
        private readonly IService<ActionLog> _actionLogService;
        private readonly DataContext _dataContext;
        public ManagerWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _actionLogService = new Service<ActionLog>(context);
            _dataContext = context;
        }

        private void CreateOrder_Click(object sender, RoutedEventArgs e)
        {
            OrderWindow ow = new OrderWindow(_currentUser, _dataContext, null);
        }

        private void OnStockFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }

        private void OnStockFilterChanged(object sender, RoutedEventArgs e)
        {

        }

        private void RefreshStock_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

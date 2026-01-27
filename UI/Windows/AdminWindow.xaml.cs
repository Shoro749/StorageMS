using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Windows;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for AdminWindow.xaml
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly User _currentUser;
        private readonly IUserService _userService;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
        }
    }
}

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
        private List<User> _users;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
            LoadUsers();
            UpdateUserList(_users);

            dg_userList.Items.Add(user);
        }

        private async Task LoadUsers()
        {
            try
            {
                _users = await _userService.GetAllAsync();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async Task UpdateUserList(List<User> users)
        {
            dg_userList.ItemsSource = users;
            dg_userList.Items.Refresh();
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            b_detailsPanel.Visibility = Visibility.Collapsed;
        }

        private void Details_Click(object sender, RoutedEventArgs e)
        {
            b_detailsPanel.Visibility = Visibility.Visible;
        }
    }
}

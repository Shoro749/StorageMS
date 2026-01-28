using Data.Context;
using Data.Models;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;
using Service.Services;
using System.Windows;
using System.Windows.Controls;

namespace UI.Windows
{
    /// <summary>
    /// Interaction logic for AdminWindow.xaml
    /// </summary>
    public partial class AdminWindow : Window
    {
        private readonly User _currentUser;
        private readonly IUserService _userService;
        private readonly IRepository<Role> _roleRepository;
        private List<User> _users;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
            _roleRepository = new Repository<Role>(context);
            this.Loaded += AdminWindow_Loaded;
        }

        private async void AdminWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.WhenAll(LoadRoles(), LoadUsers());
        }

        private async Task LoadUsers()
        {
            try
            {
                _users = await _userService.GetAllAsync();
                UpdateUserList(_users);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження користувачів: {ex.Message}");
            }
        }

        private async Task LoadRoles()
        {
            try
            {
                var roles = await _roleRepository.GetAllAsync();

                cb_userRole.ItemsSource = roles;
                cb_userRole.DisplayMemberPath = "Name";

                var filterList = new List<object>();
                filterList.Add(new { Id = 0, Name = "Всі ролі" });
                filterList.AddRange(roles);

                cb_filterRoles.ItemsSource = filterList;
                cb_filterRoles.DisplayMemberPath = "Name";
                cb_filterRoles.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження ролей: {ex.Message}");
            }
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
            Button? button = sender as Button;

            var selectedUser = button?.DataContext as User;

            if (selectedUser != null)
            {
                tb_name.Text = selectedUser.Name;
                cb_userRole.SelectedIndex = selectedUser.Role.Id - 1;
                b_detailsPanel.Visibility = Visibility.Visible;
            }
            else MessageBox.Show("Невдалося переглянути користувача!");
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

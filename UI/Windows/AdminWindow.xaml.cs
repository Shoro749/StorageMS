using Data.Context;
using Data.Models;
using Microsoft.IdentityModel.Tokens;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;
using Service.Services;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
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
        private User _chosenUser;
        private readonly IUserService _userService;
        private readonly IService<Role> _roleService;
        private readonly IService<ActionLog> _logService;
        private List<User> _users;
        public AdminWindow(User user, DataContext context)
        {
            InitializeComponent();
            _currentUser = user;
            _userService = new UserService(context);
            _roleService = new Service<Role>(context);
            _logService = new Service<ActionLog>(context);
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
                var roles = await _roleService.GetAllAsync();

                cb_userRole.ItemsSource = roles;
                cb_userRole.DisplayMemberPath = "Name";

                cb_createRole.ItemsSource = roles;
                cb_createRole.DisplayMemberPath = "Name";

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

        private void UpdateUserList(List<User> users)
        {
            dg_userList.ItemsSource = users;
            dg_userList.Items.Refresh();
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            _chosenUser = null;
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
                _chosenUser = selectedUser;
                b_detailsPanel.Visibility = Visibility.Visible;
            }
            else MessageBox.Show("Невдалося переглянути користувача!");
        }

        private async Task EditUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_name.Text.Trim();
                string password = pb_userPassword.Password.Trim();
                var role = cb_userRole.SelectedItem as Role;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("The username or password cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingUser = await _userService.GetByUsername(name);

                if (existingUser != null && existingUser.Id == _chosenUser.Id)
                {
                    MessageBox.Show($"The user with name {name} is already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newUser = new User
                {
                    Id = _chosenUser.Id,
                    Name = name,
                    PasswordHash = PasswordHasher.HashPassword(password),
                    Role = role,
                };

                var updatedUser = await _userService.UpdateAsync(newUser.Id, newUser);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has updated user {name} -> {updatedUser.Name}." });

                _users.Remove(_chosenUser);
                _users.Add(updatedUser);

                b_detailsPanel.Visibility = Visibility.Collapsed;
                _chosenUser = null;

                UpdateUserList(_users);

                MessageBox.Show("User was successfully updated!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex )
            {
                MessageBox.Show($"Error with editting user: {ex.Message}");
            }
        }

        private async Task DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _userService.DeleteAsync(_chosenUser.Id);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has deleted user {_chosenUser.Name}." });

                _users.Remove(_chosenUser);
                _chosenUser = null;
                b_detailsPanel.Visibility = Visibility.Collapsed;
                
                MessageBox.Show("User was successfully deleted!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error with deleting: " + ex.Message);
            }
        }

        private void CreateUser_Click(object sender, RoutedEventArgs e)
        {
            b_createUserPanel.Visibility = Visibility.Visible;
            cb_createRole.SelectedIndex = 2;
        }

        private async Task ConfirmCreateUser_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = tb_createName.Text.Trim();
                string password = pb_createPassword.Password.Trim();
                string confPassword = pb_confirmPassword.Password.Trim();
                var role = cb_createRole.SelectedItem as Role;

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(password))
                {
                    MessageBox.Show("The username or password cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (await _userService.GetByUsername(name) != null)
                {
                    MessageBox.Show($"The user with name {name} is already exists.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (password != confPassword)
                {
                    MessageBox.Show($"Please confirm password.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var newUser = new User
                {
                    Name = name,
                    PasswordHash = PasswordHasher.HashPassword(password),
                    Role = role,
                };

                var createdUser = await _userService.CreateAsync(newUser);
                await _logService.CreateAsync(new ActionLog { Action = $"{_currentUser.Name} has created new user {name}." });
                _users.Add(createdUser);
                b_createUserPanel.Visibility = Visibility.Collapsed;
                
                UpdateUserList(_users);

                MessageBox.Show("User was successfully created!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Невдалося створити користувача: {ex.Message}");
            }
        }

        private void CloseCreatePanel_Click(object sender, RoutedEventArgs e)
        {
            b_createUserPanel.Visibility = Visibility.Collapsed;
        }

        private void RoleFilterChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (cb_filterRoles == null || _users == null) return;

            var selected = cb_filterRoles.SelectedItem;
            if (selected == null) return;

            string roleName = selected.GetType().GetProperty("Name")?.GetValue(selected)?.ToString();

            if (string.IsNullOrEmpty(roleName) || roleName == "Всі ролі")
            {
                UpdateUserList(_users);
            }
            else
            {
                var filteredUsers = _users
                    .Where(u => u.Role != null && u.Role.Name == roleName)
                    .ToList();
                UpdateUserList(filteredUsers);
            }
        }

        private void SearchChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            string text = tb_userNameSearch.Text.Trim().ToLower();

            if (string.IsNullOrEmpty(text)) return;

            var filteredUsers = _users.Where(u => u.Name != null && u.Name.ToLower().Contains(text)).ToList();

            UpdateUserList(filteredUsers);
        }
    }
}

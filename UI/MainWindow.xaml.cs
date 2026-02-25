using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Repository.Interfaces;
using Repository.Repositories;
using Service.Interfaces;
using Service.Services;
using System.Threading.Tasks;
using System.Windows;
using UI.Windows;

namespace UI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DataContext _context;
        private readonly IUserService _userService;
        public MainWindow()
        {
            InitializeComponent();
            _context = new DataContext();
            _userService = new UserService(_context);
            Seeder();
        }

        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string login = tb_login.Text.Trim();
                string password = tb_password.Password.Trim();

                if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Please fill in all fields", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var user = await _userService.GetByUsername(login);

                if (user == null)
                {
                    MessageBox.Show("User not found", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
                {
                    MessageBox.Show("Incorrect password", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                switch (user.Role.Name)
                {
                    case "Admin":
                        AdminWindow adminWindow = new AdminWindow(user, _context);
                        Application.Current.MainWindow = adminWindow;
                        adminWindow.Show();
                        break;

                    case "Manager":
                        ManagerWindow managerWindow = new ManagerWindow(user, _context);
                        Application.Current.MainWindow = managerWindow;
                        managerWindow.Show();
                        break;

                    case "Storekeeper":
                        StorekeeperWindow storekeeperWindow = new StorekeeperWindow(user, _context);
                        Application.Current.MainWindow = storekeeperWindow;
                        storekeeperWindow.Show();
                        break;
                }
                this.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private async Task Seeder()
        {
            try
            {
                IService<Role> role = new Service<Role>(_context);

                var roles = await role.GetAllAsync();

                if (roles.Count > 0) return;

                Role role1 = new Role { Name = "Admin" };
                Role role2 = new Role { Name = "Manager" };
                Role role3 = new Role { Name = "Storekeeper" };
                
                role1 = await role.CreateAsync(role1);
                await role.CreateAsync(role2);
                await role.CreateAsync(role3);

                User admin = new User
                {
                    Name = "admin",
                    PasswordHash = PasswordHasher.HashPassword("admin"),
                    Role = role1,
                };

                await _userService.CreateAsync(admin);
            }
            catch (DbUpdateException ex)
            {
                var message = ex.InnerException?.Message ?? ex.Message;
                MessageBox.Show($"Помилка бази даних: {message}");
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        } 
    }
}
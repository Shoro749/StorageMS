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
            //Seeder();
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

                switch (user.Role.Id)
                {
                    case 1:
                        AdminWindow adminWindow = new AdminWindow(user, _context);
                        Application.Current.MainWindow = adminWindow;
                        adminWindow.Show();
                        break;

                    case 2:
                        ManagerWindow managerWindow = new ManagerWindow(user, _context);
                        Application.Current.MainWindow = managerWindow;
                        managerWindow.Show();
                        break;

                    case 3:
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
                //IService<Role> role = new Service<Role>(_context);

                //Role role1 = new Role { Name = "Admin" };
                //Role role2 = new Role { Name = "Manager" };
                //Role role3 = new Role { Name = "Storekeeper" };

                //User admin = new User
                //{
                //    Name = "admin",
                //    PasswordHash = PasswordHasher.HashPassword("admin"),
                //    Role = await role.GetByIdAsync(1),
                //};

                //await _userService.CreateAsync(admin);
                //await role.CreateAsync(role2);
                //await role.CreateAsync(role3);

                var user = await _userService.GetByUsername("Tony Pepperoni");

                var products = new List<Product>
                {
                    new Product { Name = "Кабель живлення 1.5м", Unit = "шт", Stock = 50, MinimumStock = 10, Description = "Мідний кабель" },
                    new Product { Name = "Монітор 24\" IPS", Unit = "шт", Stock = 3, MinimumStock = 5, Description = "Офісний монітор" },
                    new Product { Name = "Мишка бездротова", Unit = "шт", Stock = 0, MinimumStock = 5, Description = "Logitech B170" },
                    new Product { Name = "Клавіатура мембранна", Unit = "шт", Stock = 15, MinimumStock = 5, Description = "Стандартна USB" },
                    new Product { Name = "Патч-корд 3м", Unit = "шт", Stock = 100, MinimumStock = 20, Description = "CAT5e" }
                };
                var requests = new List<OutgoingRequest>
                {
                    new OutgoingRequest { Status = "Completed", Comment = "Для відділу маркетингу", CreatedAt = DateTime.Now.AddDays(-5), CreatedBy = user },
                    new OutgoingRequest { Status = "Pending", Comment = "Термінова заміна обладнання", CreatedAt = DateTime.Now.AddDays(-2), CreatedBy = user },
                    new OutgoingRequest { Status = "Rejected", Comment = "Не вказано причину видачі", CreatedAt = DateTime.Now.AddDays(-1), CreatedBy = user },
                    new OutgoingRequest { Status = "Completed", Comment = "Облаштування нового робочого місця", CreatedAt = DateTime.Now.AddHours(-10), CreatedBy = user },
                    new OutgoingRequest { Status = "Pending", Comment = "Запасні комплектуючі на склад", CreatedAt = DateTime.Now.AddHours(-2), CreatedBy = user }
                };

                await _context.Products.AddRangeAsync(products);
                await _context.OutgoingRequests.AddRangeAsync(requests);
                await _context.SaveChangesAsync();

                var items = new List<OutgoingItem>
                {
                    // До заявки №1
                    new OutgoingItem { Quantity = 2, Product = products[0], Request = requests[0] },
                    new OutgoingItem { Quantity = 1, Product = products[3], Request = requests[0] },
    
                    // До заявки №2
                    new OutgoingItem { Quantity = 1, Product = products[1], Request = requests[1] },
    
                    // До заявки №4
                    new OutgoingItem { Quantity = 5, Product = products[4], Request = requests[3] },
    
                    // До заявки №5
                    new OutgoingItem { Quantity = 10, Product = products[0], Request = requests[4] }
                };

                await _context.OutgoingItems.AddRangeAsync(items);
                await _context.SaveChangesAsync();
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
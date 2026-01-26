using Data.Context;
using Data.Models;
using Service.Interfaces;
using Service.Services;
using System.Threading.Tasks;
using System.Windows;

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
        }

        private async Task Login_Click(object sender, RoutedEventArgs e)
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

                //GeneralWindow generalWindow = new GeneralWindow(_context, user);
                //Application.Current.MainWindow = generalWindow;
                //generalWindow.Show();
                //this.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
    }
}
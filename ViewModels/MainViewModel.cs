using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TsdClient.Models;

namespace TsdClient.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<Product> Products { get; set; }
        public ICommand AddProductCommand { get; set; }
        public ICommand DeleteProductCommand { get; set; }
        public ICommand LoadDataCommand { get; set; }

        private Queue<string> pendingCommands = new Queue<string>();

        private string serverIp = "192.168.0.107";
        private int serverPort = 8888;

        private string? productCode;
        public string? ProductCode
        {
            get { return productCode; } set
            {
                productCode = value;
                OnPropertyChanged(nameof(ProductCode));
            }
        }

        private int productQuantity;
        public int ProductQuantity
        {
            get { return productQuantity; } set
            {
                productQuantity = value;
                OnPropertyChanged(nameof(ProductQuantity));
            }
        }

        private Product? selectedProduct;
        public Product? SelectedProduct
        {
            get { return selectedProduct; } set
            {
                selectedProduct = value;
                OnPropertyChanged(nameof(SelectedProduct));
            }
        }

        public MainViewModel()
        {
            Products = new ObservableCollection<Product>();
            AddProductCommand = new DelegateCommand(AddProductAsync);
            DeleteProductCommand = new DelegateCommand(DeleteProductAsync, CanDeleteProduct);
            LoadDataCommand = new DelegateCommand(LoadDataAsync);
        }

        private void AddProductAsync()
        {
            if (string.IsNullOrWhiteSpace(ProductCode) || ProductQuantity == 0)
                return;
            Products.Add(new Product { Code = ProductCode, Quantity = ProductQuantity });
            string command = $"+{Products.Last().Code},{Products.Last().Quantity}";

            EnqueueCommand(command);
            ProductCode = string.Empty;
            ProductQuantity = 0;
        }

        private bool CanDeleteProduct() => Products.Any() && SelectedProduct != null;

        private void DeleteProductAsync()
        {
            if(SelectedProduct != null)
            {
                string selectedProduct = SelectedProduct.Code;
                Products.Remove(SelectedProduct);
                string command = $"-{selectedProduct}";

                EnqueueCommand(command);
            }
        }

        private async void LoadDataAsync()
        {
            try
            {
                using(var client = new TcpClient())
                {
                    await client.ConnectAsync(serverIp, serverPort);
                    using(var stream = client.GetStream())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes("LOAD");
                        await stream.WriteAsync(bytes, 0, bytes.Length);

                        byte[] buffer = new byte[1024];
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        string data = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        UpdateProductsFromData(data);
                    }
                }

                Console.WriteLine("Data loaded successfully.");
            } catch(Exception ex)
            {
                Console.WriteLine("Error loading data: " + ex.Message);
            }
        }

        private void UpdateProductsFromData(string data)
        {
            Products.Clear();

            string[] lines = data.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string line in lines)
            {
                string[] parts = line.Split(',');
                string productCode = parts[0];
                int quantity = int.Parse(parts[1]);

                Products.Add(new Product { Code = productCode, Quantity = quantity });
            }
        }

        private async Task SendCommandToServerAsync(string command)
        {
            using(var client = new TcpClient())
            {
                await client.ConnectAsync(serverIp, serverPort);

                using (var stream = client.GetStream())
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(command);
                    await stream.WriteAsync(bytes, 0, bytes.Length);
                }
            }
        }

        private void EnqueueCommand(string command)
        {
            pendingCommands.Enqueue(command);
            Task.Run(() => ProcessPendingCommandsAsync());
        }

        private async Task ProcessPendingCommandsAsync()
        {
            while (pendingCommands.Any())
            {
                string command = pendingCommands.Dequeue();
                try
                {
                    await SendCommandToServerAsync(command);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error sending command: " + ex.Message);
                    pendingCommands.Enqueue(command);
                    await Task.Delay(1000);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

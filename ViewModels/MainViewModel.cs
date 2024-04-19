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

        private string serverIp = "192.168.3.80";
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
            AddProductCommand = new DelegateCommand(AddProduct);
            DeleteProductCommand = new DelegateCommand(DeleteProduct, CanDeleteProduct);
            LoadDataCommand = new DelegateCommand(LoadData);
        }

        private void AddProduct()
        {
            if (string.IsNullOrWhiteSpace(ProductCode) || ProductQuantity == 0)
                return;
            Products.Add(new Product { Code = ProductCode, Quantity = ProductQuantity });
            SendCommandToServer($"+{Products.Last().Code},{Products.Last().Quantity}");
            ProductCode = string.Empty;
            ProductQuantity = 0;
        }

        private bool CanDeleteProduct() => Products.Any() && SelectedProduct != null;

        private void DeleteProduct()
        {
            if(SelectedProduct != null)
            {
                string selectedProduct = SelectedProduct.Code;
                Products.Remove(SelectedProduct);
                SendCommandToServer($"-{selectedProduct}");
            }
        }

        private void LoadData()
        {
            try
            {
                using(var client = new TcpClient(serverIp, serverPort))
                {
                    using(var stream = client.GetStream())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes("LOAD");
                        stream.Write(bytes, 0, bytes.Length);

                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
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

        private void SendCommandToServer(string command)
        {
            try
            {
                using(var client = new TcpClient(serverIp, serverPort))
                {
                    using (var stream = client.GetStream())
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(command);
                        stream.Write(bytes, 0, bytes.Length);
                    }
                }
            } catch (Exception ex)
            {
                Console.WriteLine("Error sending command to server: " + ex.Message);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

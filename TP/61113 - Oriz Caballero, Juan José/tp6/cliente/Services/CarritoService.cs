using System.Net.Http.Json;
using Blazored.LocalStorage;
using Cliente.Modelo;
using Microsoft.AspNetCore.Components;

namespace Cliente.Services
{
    public class CarritoService
    {
        private readonly HttpClient _http;
        private readonly ILocalStorageService _localStorage;
        private int _carritoId;
        public int TotalItems { get; private set; } = 0;
        private CarritoDto carrito = new();

        public CarritoService(HttpClient http, ILocalStorageService localStorage)
        {
            _http = http;
            _localStorage = localStorage;
        }

        public async Task<CarritoDto> ObtenerCarrito()
        {
            await AsegurarCarritoId();

            if (_carritoId == 0)
            {
                Console.WriteLine("❌ carritoId inválido");
                return new CarritoDto();
            }

            var response = await _http.GetAsync($"carritos/{_carritoId}");

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ Error al obtener carrito: {response.StatusCode}");
                return new CarritoDto();
            }

            carrito = await response.Content.ReadFromJsonAsync<CarritoDto>() ?? new CarritoDto();
            TotalItems = carrito.Items.Sum(i => i.Cantidad);
            return carrito;
        }

        public async Task AgregarProducto(int productoId, int cantidad)
        {
            await AsegurarCarritoId();

            var carritoActual = await ObtenerCarrito();
            var itemExistente = carritoActual.Items.FirstOrDefault(i => i.ProductoId == productoId);
            int cantidadFinal = (itemExistente?.Cantidad ?? 0) + cantidad;

            var response = await _http.PutAsJsonAsync($"carritos/{_carritoId}/{productoId}", cantidadFinal);
            response.EnsureSuccessStatusCode();

            carrito = await response.Content.ReadFromJsonAsync<CarritoDto>() ?? new CarritoDto();
            TotalItems = carrito.Items.Sum(i => i.Cantidad);
        }

        public async Task ActualizarCantidad(int productoId, int nuevaCantidad)
        {
            await AsegurarCarritoId();

            var response = await _http.PutAsJsonAsync($"carritos/{_carritoId}/{productoId}", nuevaCantidad);
            response.EnsureSuccessStatusCode();

            carrito = await response.Content.ReadFromJsonAsync<CarritoDto>() ?? new CarritoDto();
            TotalItems = carrito.Items.Sum(i => i.Cantidad);
        }

        public async Task EliminarProducto(int productoId)
        {
            await AsegurarCarritoId();

            var item = carrito.Items.FirstOrDefault(i => i.ProductoId == productoId);
            if (item == null) return;

            var response = await _http.DeleteAsync($"carritos/{_carritoId}/{productoId}?cantidad={item.Cantidad}");
            response.EnsureSuccessStatusCode();

            carrito = await ObtenerCarrito();
        }

        public async Task VaciarCarrito()
        {
            await AsegurarCarritoId();

            var response = await _http.DeleteAsync($"carritos/{_carritoId}");
            response.EnsureSuccessStatusCode();

            carrito = await ObtenerCarrito();
            TotalItems = 0;
        }

        public async Task VaciarCarrito(NavigationManager nav)
        {
            await VaciarCarrito();
            nav.NavigateTo("/");
        }

        public async Task<bool> ConfirmarCompra(ConfirmacionCompraDto datos)
        {
            await AsegurarCarritoId();

            var response = await _http.PutAsJsonAsync($"carritos/{_carritoId}/confirmar", datos);
            if (!response.IsSuccessStatusCode) return false;

            carrito = new CarritoDto { Id = _carritoId, Items = new List<ItemCarritoDto>() };
            TotalItems = 0;
            await _localStorage.SetItemAsync("carritoId", _carritoId);
            return true;
        }

        public async Task CargarCarrito()
        {
            carrito = await ObtenerCarrito();
        }

        private async Task AsegurarCarritoId()
        {
            if (_carritoId != 0) return;

            var guardado = await _localStorage.GetItemAsync<int>("carritoId");
            if (guardado != 0)
            {
                _carritoId = guardado;
                return;
            }

            var response = await _http.PostAsync("carritos", null);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"❌ Error al crear carrito: {response.StatusCode}");
                return;
            }

            var datos = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
            if (datos == null || !datos.ContainsKey("CarritoId"))
            {
                Console.WriteLine("❌ No se pudo obtener el ID del carrito.");
                return;
            }

            _carritoId = datos["CarritoId"];
            await _localStorage.SetItemAsync("carritoId", _carritoId);
        }
    }
}

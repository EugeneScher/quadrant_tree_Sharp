using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Microsoft.Maps.MapControl.WPF;
using System.Net.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Map
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const string BingMapsApiKey = "AhLSabxwJuk9sPhEnJhb3cJjg2TVz73gBsRFjl2OkfMvnzdqSsxwo3ZdPjD-9Izi";
        private LocationCollection routeLocations = new LocationCollection();

        public MainWindow()
        {
            InitializeComponent();

            MyMap.MouseLeftButtonDown += Map_MouseLeftButtonDown;
            MyMap.MouseRightButtonDown += Map_MouseRightButtonDown;
        }

        private async void Map_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point mousePosition = e.GetPosition(MyMap);
            Location location = MyMap.ViewportPointToLocation(mousePosition);
            routeLocations.Add(location);

            await UpdateRouteAsync();
        }

        private void Map_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            //// Очистить маршрут при правом клике
            //routeLocations.Clear();
            //MyMap.Children.Clear(); // Очистить все элементы на карте
        }

        private async System.Threading.Tasks.Task UpdateRouteAsync()
        {
            if (routeLocations.Count <= 2)
            {
                // Получение маршрута от Bing Maps API
                string waypoints = string.Join(";", routeLocations);
                string requestUrl = $"https://dev.virtualearth.net/REST/V1/Routes/Driving?wayPoint.1={waypoints}&key={BingMapsApiKey}";
                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = await client.GetAsync(requestUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        dynamic data = JsonConvert.DeserializeObject(json);
                        List<Location> routePoints = new List<Location>();
                        foreach (var point in data.resourceSets[0].resources[0].routePath.line.coordinates)
                        {
                            routePoints.Add(new Location((double)point[0], (double)point[1]));
                        }

                        MapPolyline route = new MapPolyline();
                        route.Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Blue);
                        route.StrokeThickness = 5;
                        route.Opacity = 0.7;
                        LocationCollection routeLocations = new LocationCollection();

                        foreach (var point in routePoints)
                        {
                            routeLocations.Add(point);
                        }

                        route.Locations = routeLocations;

                        MyMap.Children.Clear();
                        MyMap.Children.Add(route);
                    }
                    else
                    {
                        await ShowErrorMessageBox("Не удалось получить маршрут.");
                    }
                }
            }
        }
        private async Task ShowErrorMessageBox(string message)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void ClearMap_Click(object sender, RoutedEventArgs e)
        {
            MyMap.Children.Clear();
            routeLocations.Clear();
        }

        private void AddLocation_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(LatitudeTextBox.Text, out double latitude) && double.TryParse(LongitudeTextBox.Text, out double longitude))
            {
                Location location = new Location(latitude, longitude);

                Pushpin pin = new Pushpin();
                pin.Location = location;

                MyMap.Children.Add(pin);
                MyMap.SetView(location, 12); // Указывает позицию на карте с заданным уровнем масштабирования
                routeLocations.Add(location);
            }
        }

        private async void ShowRoute_Click(object sender, RoutedEventArgs e)
        {
            await UpdateRouteAsync();
        }

        private void Map_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Point mousePosition = e.GetPosition(MyMap);
            Location location = MyMap.ViewportPointToLocation(mousePosition);

            Pushpin pin = new Pushpin();
            pin.Location = location;

            MyMap.Children.Add(pin);
            MyMap.SetView(location, 12); // Установка позиции на карте с заданным уровнем масштабирования
            routeLocations.Add(location);
        }

        private void ClearQuadTree()
        {
            foreach (var quadTree in quadTreeCollection)
            {
                foreach (var poly in quadTree)
                {
                    QuadTreeLayer.Children.Remove(poly); // Удаляем отрезок со слоя карты
                }
            }

            quadTreeCollection.Clear(); // Очищаем коллекцию деревьев
        }
        private List<List<MapPolyline>> quadTreeCollection = new List<List<MapPolyline>>();

        private void BuildQuadTree_Click(object sender, RoutedEventArgs e)
        {
            ClearQuadTree(); // Очистка предыдущих квадрантов перед построением новых

            double north = MyMap.Center.Latitude + MyMap.ZoomLevel;
            double south = MyMap.Center.Latitude - MyMap.ZoomLevel;
            double east = MyMap.Center.Longitude + MyMap.ZoomLevel;
            double west = MyMap.Center.Longitude - MyMap.ZoomLevel;

            // Здесь можно использовать данные границы и разбивать пространство на квадранты
            // Пример: разбиение на 4 квадранта (северо-восток, северо-запад, юго-восток, юго-запад)
            double verticalMid = (north + south) / 2;
            double horizontalMid = (east + west) / 2;

            Location northEast = new Location(north, east);
            Location northWest = new Location(north, west);
            Location southEast = new Location(south, east);
            Location southWest = new Location(south, west);

            // Создание и добавление отрезков для отображения границ квадрантов
            MapPolyline polyNorth = new MapPolyline();
            polyNorth.Stroke = new SolidColorBrush(Colors.Red);
            polyNorth.StrokeThickness = 2;
            polyNorth.Opacity = 0.7;
            polyNorth.Locations = new LocationCollection { northEast, northWest };

            // Добавление созданных отрезков к QuadTreeLayer
            QuadTreeLayer.Children.Add(polyNorth);

            // Отрезок между northEast и northWest
            MapPolyline polyNorthWest = new MapPolyline();
            polyNorthWest.Stroke = new SolidColorBrush(Colors.Blue);
            polyNorthWest.StrokeThickness = 2;
            polyNorthWest.Opacity = 0.7;
            polyNorthWest.Locations = new LocationCollection { northWest, southWest };
            QuadTreeLayer.Children.Add(polyNorthWest);

            // Отрезок между southWest и northWest
            MapPolyline polySouthWest = new MapPolyline();
            polySouthWest.Stroke = new SolidColorBrush(Colors.Yellow);
            polySouthWest.StrokeThickness = 2;
            polySouthWest.Opacity = 0.7;
            polySouthWest.Locations = new LocationCollection { southWest, northWest };
            QuadTreeLayer.Children.Add(polySouthWest);

            // Отрезок между southWest и southEast
            MapPolyline polySouthEast = new MapPolyline();
            polySouthEast.Stroke = new SolidColorBrush(Colors.Orange);
            polySouthEast.StrokeThickness = 2;
            polySouthEast.Opacity = 0.7;
            polySouthEast.Locations = new LocationCollection { southEast, southWest };
            QuadTreeLayer.Children.Add(polySouthEast);
        }

        private void WindowMinimizeClick(object sender, RoutedEventArgs e)
        {
            Application.Current.MainWindow.WindowState = WindowState.Minimized;
        }

        private void WindowMaximizeClick(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow.WindowState != WindowState.Maximized)
            {
                Application.Current.MainWindow.WindowState = WindowState.Maximized;
            }
            else
                Application.Current.MainWindow.WindowState = WindowState.Normal;
        
    }

        private void WindowCloseClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }
    }
}

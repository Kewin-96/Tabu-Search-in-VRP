using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace TabuVRP002
{
    public static class DrawX   // Graphical representation /// Reprezentacja graficzna
    {
        public static Grid mainGrid;
        public static int clientsCount; // Clients Count /// Liczba klientów
        public static List<List<Line>> tracksLines = new List<List<Line>>();    // Track lines /// Linie pokazujące trasy
        public static Ellipse[] clientsCircles = new Ellipse[0];     // Clients (circles) /// Klienci (koła)
        public static Brush[] colors = { Brushes.Red, Brushes.Orange, Brushes.Yellow, Brushes.Green, Brushes.Cyan, Brushes.Blue, Brushes.Purple, Brushes.Brown, Brushes.Gray, Brushes.Black };
        public static int thickness = 3;    // Lines thickness /// Grubość linii
        public static Point main_station;   // Main station (shipping point) /// Główna stacja (Punkt wysyłkowy)
        public static Ellipse main_station_ellipse;

        public static void SendData(Grid grid, int clientsCountx)
        {
            mainGrid = grid;
            clientsCount = clientsCountx;
        }
        public static void DrawMainStation(Point ms)   // Drawing main station /// Rysowanie głównej stacji
        {
            if (main_station != null)
                mainGrid.Children.Remove(main_station_ellipse);
            main_station = ms;
            main_station_ellipse = new Ellipse();
            main_station_ellipse.Height = 10;
            main_station_ellipse.Width = 10;
            main_station_ellipse.Margin = new Thickness(main_station.x-5, main_station.y-5, 0, 0);
            main_station_ellipse.Stroke = Brushes.Black;
            main_station_ellipse.Fill = Brushes.Red;
            main_station_ellipse.HorizontalAlignment = HorizontalAlignment.Left;
            main_station_ellipse.VerticalAlignment = VerticalAlignment.Top;
            mainGrid.Children.Add(main_station_ellipse);
        }
        public static void DrawClients(Point[] clients)   // Drawing clients (delivery points) /// Rysowanie klientów (punktów dostaw)
        {
            clientsCircles = new Ellipse[clientsCount];
            for (int i = 0; i < clientsCount; i++)
            {
                Ellipse ellipse = new Ellipse();
                ellipse.Height = 10;
                ellipse.Width = 10;
                ellipse.Margin = new Thickness(clients[i].x - 5, clients[i].y - 5, 0, 0);
                ellipse.Stroke = Brushes.Blue;
                ellipse.HorizontalAlignment = HorizontalAlignment.Left;
                ellipse.VerticalAlignment = VerticalAlignment.Top;
                clientsCircles[i] = ellipse;
                mainGrid.Children.Add(clientsCircles[i]);
            }
        }
        public static void DrawLines(List<List<Point>> tracks) // Drawing lines /// Rysowanie linii
        {
            mainGrid.Dispatcher.Invoke(() =>
            {
                tracksLines = new List<List<Line>>();
                for (int i = 0; i < tracks.Count; i++)
                {
                    tracksLines.Add(new List<Line>());
                    for (int j = 0; j < tracks[i].Count - 1; j++)
                    {
                        Line line = new Line();
                        line.X1 = tracks[i][j].x;
                        line.Y1 = tracks[i][j].y;
                        line.X2 = tracks[i][j + 1].x;
                        line.Y2 = tracks[i][j + 1].y;
                        line.StrokeThickness = thickness;
                        line.Stroke = colors[i % 10];
                        tracksLines[i].Add(line);
                        mainGrid.Children.Add(tracksLines[i][j]);
                    }
                }
            });
        }
        public static void RemoveLines()   // Removing lines /// Usuwanie linii
        {
            mainGrid.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < tracksLines.Count; i++)
                {
                    for (int j = 0; j < tracksLines[i].Count; j++)
                    {
                        mainGrid.Children.Remove(tracksLines[i][j]);
                    }
                }
            });
        }
        public static void RemoveClients()   // Removing clients (circles) /// Usuwanie klientów (kółek)
        {
            mainGrid.Dispatcher.Invoke(() =>
            {
                for (int i = 0; i < clientsCircles.Length; i++)
                {
                    mainGrid.Children.Remove(clientsCircles[i]);
                }
            });
        }
    }
}

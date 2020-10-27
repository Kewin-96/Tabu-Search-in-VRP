using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TabuVRP002
{
    public partial class MainWindow : Window
    {
        // Parameters /// Parametry
        public static int minMass = 3;
        public static int maxMass = 20;
        public static int clientsCount = 30;
        public static int capacity = 50;
        public static int cadenceMod = 10; // Cadence modifier /// Modyfikator kadencji
        public static double percMaxTabu = 0.1; // Percentage of forbidden moves (ratio of the maximum number of forbidden moves to all possible moves) /// Procent zakazanych ruchów (stosunek maksymalnej ilości zakazanych ruchów do wszystkich możliwych ruchów)
        public static int aspirationPlus = 500;
        public static int aspirationPlusPlus = 5;
        public static double aspiration = 0.9;

        // Derived parameters /// Parametry pochodne
        public static int cadence = (int)(clientsCount * (cadenceMod / 100.0)); // Cadence length [iterations] /// Długość kadencji [w iteracjach]
        public static int avaMoves = ((clientsCount - 2) * (clientsCount - 3)) / 2; // Number of possible moves /// Ilość możliwych ruchów
        public static int maxTabu = (int)((percMaxTabu * 0.01) * avaMoves); // Maximum number of forbidden moves /// Maksymalna ilość zakazanych ruchów

        // Input data /// Dane wejściowe
        public static Point[] clients; // Clients set (circles) /// Zbiór klientów (circles)
        public static int[] clientsOrders; // Masses of orders /// Masy zamówień

        // Output data /// Dane wyjściowe
        public static List<List<Point>> tracks = new List<List<Point>>(); // Trakcs /// Trasy
        public static List<List<Point>> bestTracks = new List<List<Point>>(); // Best tracks /// Najlepsze trasy
        public static double bestOfBestCost = 1e100; // Best instantaneous cost of Hamilton cycle /// Najlepszy koszt chwilowy cyklu Hamiltona
        public static double finalCost = 1e100; // Final cost /// Koszt finalny

        // Tabu list /// Lista Tabu
        public static List<int[]> tabuList = new List<int[]>();

        // Miscellaneous /// Inne
        Random rnd = new Random();
        Point main_station = new Point(500, 500);
        bool windowInited = false;
        bool debug = false;

        // Parameters tests /// Testy parametrow:
        public static double[] percMaxTabu_T = { 0.1, 1, 10 };
        public static int[] cadenceMod_T = { 1, 10, 100 };
        public static int[] aspirationPlus_T = { 10, 100, 2000 };
        public static int[] aspirationPlusPlus_T = { 3, 5, 10 };
        /*public static double[] percMaxTabu_T = { 0.1, 0.5, 1, 5, 10 };
        public static int[] cadenceMod_T = { 1, 10, 25, 50, 100 };
        public static int[] aspirationPlus_T = { 10, 50, 100, 500, 2000 };
        public static int[] aspirationPlusPlus_T = { 3, 5, 10, 15, 25 };*/

        public MainWindow()
        {
            InitializeComponent();
            windowInited = true;
            DrawX.SendData(mainGrid, clientsCount);
            DrawX.DrawMainStation(main_station);
            string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            try
            {
                new StreamReader(System.IO.Path.Combine(exePath, "plik1.txt"));
            }
            catch (FileNotFoundException e)
            {
                load_Button.IsEnabled = false;
            }
        }
        private void Start()
        {
            UpdateParams();

            int iteracjeBezPoprawy = aspirationPlusPlus;
            int iteracjaTotal = 1;
            bestOfBestCost = 1e100;
            finalCost = 1e100;
            iteration_textBlock.Dispatcher.Invoke(() => { iteration_textBlock.Text = "0"; });
            cost_textBlock.Dispatcher.Invoke(() => { cost_textBlock.Text = "0"; });
            bestCost_textBlock.Dispatcher.Invoke(() => { bestCost_textBlock.Text = "0"; });
            finalCost_textBlock.Dispatcher.Invoke(() => { finalCost_textBlock.Text = "0"; });
            while (iteracjeBezPoprawy > 0)
            {
                // *** losowanie cyklu Hamiltona ***
                bool bufBB = false;
                kNNStart_checkBox.Dispatcher.Invoke(() => { bufBB = (bool)(kNNStart_checkBox.IsChecked); });
                if (bufBB == false)
                {
                    tracks = new List<List<Point>>();
                    tracks.Add(new List<Point>());
                    List<Point> bufCliets = new List<Point>();
                    for (int i = 0; i < clientsCount; i++)
                    {
                        bufCliets.Add(clients[i]);
                    }
                    for (int i = 0; i < clientsCount; i++)
                    {
                        int bufr = rnd.Next(0, bufCliets.Count);
                        tracks[0].Add(bufCliets[bufr]);
                        bufCliets.RemoveAt(bufr);
                    }
                    tracks[0].Add(tracks[0][0]);
                }
                else
                {
                    tracks = new List<List<Point>>();
                    tracks.Add(new List<Point>());
                    List<Point> bufCliets = new List<Point>();
                    for (int i = 0; i < clientsCount; i++)
                    {
                        bufCliets.Add(clients[i]);
                    }
                    int bufr = rnd.Next(0, clientsCount);
                    tracks[0].Add(bufCliets[bufr]);
                    bufCliets.RemoveAt(bufr);
                    for (int i = 0; i < clientsCount - 1; i++)
                    {
                        double bufbestL = 2000 * clientsCount + 1;
                        int bestInd = -1;
                        for (int j = 0; j < bufCliets.Count; j++)
                        {
                            double bufD = LengthEuclid(tracks[0][i], bufCliets[j]);
                            if (bufD < bufbestL)
                            {
                                bufbestL = bufD;
                                bestInd = j;
                            }
                        }
                        tracks[0].Add(bufCliets[bestInd]);
                        bufCliets.RemoveAt(bestInd);
                    }
                    tracks[0].Add(tracks[0][0]);

                }

                // *** inicjalizacja zmiennych do algorytmu Tabu Search ***
                double bufbestCost = 2000 * clientsCount + 1, buf;
                int bufbestIndex1 = -1, bufbestIndex2 = -1;
                double bufworstCost = 0;
                int bufworstIndex1 = -1, bufworstIndex2 = -1;
                int iteracja = 1;
                iteration_textBlock.Dispatcher.Invoke(() => { iteration_textBlock.Text = iteracjaTotal.ToString(); });
                double bestCost = CalcHamiltonTrackLengh(tracks[0]);
                cost_textBlock.Dispatcher.Invoke(() => { cost_textBlock.Text = bestCost.ToString("F3"); });
                bool bufBreak = false, aspPlus = false;
                int iPlus = 0;
                bool paraTabu = false;

                // *** wyliczanie pierwszego kosztu ***
                cost1_textBlock.Dispatcher.Invoke(() => { cost1_textBlock.Text = bestCost.ToString("F3"); });

                // *** rysowanie wylosowanego cyklu Hamiltona ***
                DrawX.RemoveLines();
                DrawX.DrawLines(tracks);

                // *** zmiana koloru kwadratu na jasny niebieski po poprzednich obliczeniach ; opóźnienie po losowaniu i przed rozpoczęciem algorytmu
                result_Rectangle.Dispatcher.Invoke(() => { result_Rectangle.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x71, 0xFF)); });

                //DEBUG DEBUG DEBUG DEBUG DEBUG 
                if (debug) Console.WriteLine("max ilosc Tabu listy = " + maxTabu);
                if (debug) Console.WriteLine("ilosc możliwych ruchów = " + avaMoves);
                if (debug) Console.WriteLine("max procent ruchow zakazanych = " + percMaxTabu);
                if (debug) Console.WriteLine("kadencja = " + cadence);

                while (true)
                {
                    if (debug) Console.WriteLine("Iteracja: " + iteracja);

                    // *** reset zmiennych buforowych ***
                    bufbestCost = 2000 * clientsCount + 1;
                    bufbestIndex1 = -1;
                    bufbestIndex2 = -1;
                    bufworstCost = bestCost;
                    bufworstIndex1 = -1;
                    bufworstIndex2 = -1;
                    bufBreak = false;
                    aspPlus = false;
                    iPlus = -1;

                    // *** sprawdzanie wszystkich sąsiadów z ograniczeniem aspiracji plus ***
                    for (int i = 1; i < clientsCount - 1; i++)
                    {
                        for (int j = i + 1; j < clientsCount - 1; j++)
                        {
                            if (iPlus == aspirationPlus)
                            {
                                bufBreak = true;
                                break;
                            }
                            buf = CalcHamiltonTrackNeighLengh(tracks[0], i, j);
                            paraTabu = false;
                            for (int k = 0; k < tabuList.Count; k++)
                            {
                                if (tabuList[k][0] == i && tabuList[k][1] == j)
                                {
                                    paraTabu = true;
                                    break;
                                }
                            }
                            if (buf < bufbestCost && !paraTabu || buf < bufbestCost * aspiration)  //znalezione lepsze rozwiązanie w danej interacji niz najlepsze w danej iteracji
                            {
                                bufbestCost = buf;
                                bufbestIndex1 = i;
                                bufbestIndex2 = j;
                                if (buf < bestCost) // po znalezieniu lepszego rozwiązania od globalnego, startujemy aspirację plus
                                    aspPlus = true;
                            }
                            else if (buf > bufworstCost && tabuList.Count < maxTabu && !paraTabu) //znalezione gorsze rozwiązanie w danej interacji niz najgorsze w danej iteracji
                            {
                                bufworstCost = buf;
                                bufworstIndex1 = i;
                                bufworstIndex2 = j;
                            }
                            if (aspPlus)
                            {
                                iPlus++;
                            }
                        }
                        if (bufBreak)
                            break;
                    }

                    // *** warunek stopu danego podejscia ***
                    if (bestCost <= bufbestCost)
                    {
                        break;
                    }
                    else    // *** zamieniamy miejscami punkty najlepszej znalezionej pary, usuwamy zakazane ruchy, ktorych kadencja minęła oraz dodajemy do TabuList pare ***
                    {
                        bestCost = bufbestCost;
                        Point bufPoint = tracks[0][bufbestIndex1];
                        tracks[0][bufbestIndex1] = tracks[0][bufbestIndex2];
                        tracks[0][bufbestIndex2] = bufPoint;
                        for (int i = 0; i < tabuList.Count; i++)    // pomniejszanie kadencji o 1 i usuwanie elementow z kadencją = 0
                        {
                            tabuList[i][2] -= 1;
                            if (tabuList[i][2] == 0)
                                tabuList.RemoveAt(i);
                        }
                        if (tabuList.Count < maxTabu && bufworstCost > bufbestCost)    //jesli lista Tabu nie jest pełna i znalezione najogrsze rozwiązanie w iteracji jest gorsze od najlepszego rozwiązania
                        {
                            int[] bufP = { bufworstIndex1, bufworstIndex2, cadence };
                            tabuList.Add(bufP);
                        }
                    }

                    // *** rysowanie cyklu Hamiltona ***
                    DrawX.RemoveLines();
                    DrawX.DrawLines(tracks);

                    // *** wyswietlanie kosztu ***
                    cost_textBlock.Dispatcher.Invoke(() => { cost_textBlock.Text = bestCost.ToString("F3"); });

                    // *** iteracja ++ ***
                    iteracja++;
                    iteracjaTotal++;
                    iteration_textBlock.Dispatcher.Invoke(() => { iteration_textBlock.Text = iteracjaTotal.ToString(); });

                    // *** opóźnienie ***
                    delay_checkBox.Dispatcher.Invoke(() =>
                    {
                        if (delay_checkBox.IsChecked == true)
                            Thread.Sleep(500);
                    });
                }
                if (bestCost < bestOfBestCost)
                {
                    iteracjeBezPoprawy = aspirationPlusPlus;
                    if (bestTracks.Count == 1)
                        { if (debug) Console.Write(CalcHamiltonTrackLengh(bestTracks[0]) + " != "); }
                    else if (bestTracks.Count != 0)
                        throw new Exception("Bardzo zle: " + bestTracks.Count);
                    bestOfBestCost = bestCost;
                    bestCost_textBlock.Dispatcher.Invoke(() => { bestCost_textBlock.Text = bestOfBestCost.ToString("F3"); });
                    bestTracks = new List<List<Point>>();
                    bestTracks.Add(new List<Point>());
                    for (int i = 0; i < tracks[0].Count; i++)
                        bestTracks[0].Add(tracks[0][i]);
                    if (bestTracks.Count == 1)
                        { if (debug) Console.WriteLine(CalcHamiltonTrackLengh(bestTracks[0])); }
                }
                else
                {
                    iteracjeBezPoprawy--;
                }
            }

            // *** wyswietlenie najlepszego cyklu Hamiltona ***
            DrawX.RemoveLines();
            DrawX.DrawLines(bestTracks);

            // *** kwadrat na zielono ***
            result_Rectangle.Dispatcher.Invoke(() => { result_Rectangle.Fill = Brushes.LightGreen; });

            // *** pokrojenie cyklu Hamiltona ***
            double bufBestCost = 1e100;
            for (int i = 0; i < clientsCount; i++)      //iteracja po klientach (sprawdzamy kazdy punkt startowy cięć)
            {
                List<List<Point>> bufTracks = new List<List<Point>>();
                int ii = 0; //nr trasy
                int jj = 0; //nr punktu z najlepszego cyklu Hamiltona
                int bufLadunekCzesc = -1;
                while (true)
                {
                    int bufCap = capacity;
                    bufTracks.Add(new List<Point>());
                    bufTracks[ii].Add(main_station);
                    while (true)
                    {
                        if (bufLadunekCzesc > -1)
                        {
                            bufTracks[ii].Add(bestTracks[0][jj]);
                            bufCap -= bufLadunekCzesc;
                            bufLadunekCzesc = -1;
                            jj++;
                        }
                        else if (clientsOrders[bestTracks[0][jj].i] < bufCap)
                        {
                            bufTracks[ii].Add(bestTracks[0][jj]);
                            bufCap -= clientsOrders[bestTracks[0][jj].i];
                            jj++;
                        }
                        else if(clientsOrders[bestTracks[0][jj].i] == bufCap)
                        {
                            bufTracks[ii].Add(bestTracks[0][jj]);
                            bufCap = 0;
                            jj++;
                            break;
                        }
                        else
                        {
                            bufTracks[ii].Add(bestTracks[0][jj]);
                            bufLadunekCzesc = bufCap;
                            bufCap = 0;
                            break;
                        }
                        if (jj == bestTracks[0].Count - 1)
                            break;
                    }
                    bufTracks[ii].Add(main_station);
                    if (jj == bestTracks[0].Count - 1)      // minus 1 bo na końcu cyklu Hamiltona jest ten sam punkt co na początku a nie chcemy robić podwójnej dostawy do tego klienta ;)
                        break;
                    ii++;
                }

                // sprawdzenie kosztu bufTracks i jesli lepsze to przepisanie bufTracks do tracks
                double buf = CalcTracksLengh(bufTracks);
                if (bufBestCost > buf)
                {
                    bufBestCost = buf;
                    tracks = new List<List<Point>>();
                    for (int j = 0; j < bufTracks.Count; j++)
                    {
                        tracks.Add(new List<Point>());
                        for (int k = 0; k < bufTracks[j].Count; k++)
                        {
                            tracks[j].Add(bufTracks[j][k]);
                        }
                    }
                }
            }

            int buf001 = 0, buf002 = 0;
            for (int i = 0; i < clientsOrders.Length; i++)
            {
                buf001 += clientsOrders[i];
            }
            for (int i = 0; i < tracks.Count; i++)
            {
                for (int j = 1; j < tracks[i].Count-1; j++)
                {
                    if (i > 0)
                    {
                        if (j == 1 && tracks[i - 1][tracks[i - 1].Count - 2].i != tracks[i][j].i)
                        {
                            buf002 += clientsOrders[tracks[i][j].i];
                        }
                        else if (j > 1)
                        {
                            buf002 += clientsOrders[tracks[i][j].i];
                        }
                    }
                    else
                    {
                        buf002 += clientsOrders[tracks[i][j].i];
                    }
                }
            }
            if (buf001 != buf002)
                { if (debug) Console.WriteLine(" UWAGA BŁĄD: " + buf001 + " != " + buf002); }

            // *** wyswietlenie ostatecznego kosztu ***
            finalCost = CalcTracksLengh(tracks);
            finalCost_textBlock.Dispatcher.Invoke(() => { finalCost_textBlock.Text = finalCost.ToString("F3"); });

            // *** rysowanie ostateczne ***
            DrawX.RemoveLines();
            DrawX.DrawLines(tracks);

            // *** kwadrat na zielono ***
            result_Rectangle.Dispatcher.Invoke(() => { result_Rectangle.Fill = Brushes.Green; });

            // *** Aktywacja wszystkich kontrolek ***
            mainGrid.Dispatcher.Invoke(() =>
            {
                EnableAllControls();
                start_Button.IsEnabled = true;
            });
        }
        private void UpdateParams()
        {
            clientCount_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(clientCount_textBox.Text, out clientsCount)) throw new Exception(); });
            avaMoves = ((clientsCount - 2) * (clientsCount - 3)) / 2;
            DrawX.SendData(mainGrid, clientsCount);
            cadenceMod_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(cadenceMod_textBox.Text, out cadenceMod)) throw new Exception(); });
            cadence = (int)(clientsCount * (cadenceMod / 100.0));
            percMaxTabu_textBox.Dispatcher.Invoke(() => { if (!Double.TryParse(percMaxTabu_textBox.Text, out percMaxTabu)) throw new Exception(); });
            maxTabu = (int)((percMaxTabu * 0.01) * avaMoves);
            cap_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(cap_textBox.Text, out capacity)) throw new Exception(); });
            min_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(min_textBox.Text, out minMass)) throw new Exception(); });
            max_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(max_textBox.Text, out maxMass)) throw new Exception(); });
            aspirationPlus_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(aspirationPlus_textBox.Text, out aspirationPlus)) throw new Exception(); });
            aspirationPlusPlus_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(aspirationPlusPlus_textBox.Text, out aspirationPlusPlus)) throw new Exception(); });
        }
        private double LengthEuclid(Point a, Point b)
        {
            return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
        }
        private double CalcHamiltonTrackLengh(List<Point> graph)
        {
            double length = 0;
            for (int i = 1; i < graph.Count; i++)
            {
                length += LengthEuclid(graph[i - 1], graph[i]);
            }
            return length;
        }
        private double CalcTracksLengh(List<List<Point>> graph)
        {
            double length = 0;
            for (int i = 0; i < graph.Count; i++)
            {
                length += CalcHamiltonTrackLengh(graph[i]);
            }
            return length;
        }
        private double CalcHamiltonTrackNeighLengh(List<Point> graph, int index1, int index2)
        {
            double length = 0;
            for (int i = 0; i < graph.Count-1; i++)
            {
                if ((index1 == (index2 - 1)) && i == index1)
                {
                    length += LengthEuclid(graph[index2], graph[index1]);
                }
                else if (i == index1 - 1)
                {
                    length += LengthEuclid(graph[i], graph[index2]);
                }
                else if (i == index1)
                {
                    length += LengthEuclid(graph[index2], graph[i + 1]);
                }
                else if (i == index2 - 1)
                {
                    length += LengthEuclid(graph[i], graph[index1]);
                }
                else if (i == index2)
                {
                    length += LengthEuclid(graph[index1], graph[i + 1]);
                }
                else
                    length += LengthEuclid(graph[i], graph[i+1]);
            }
            return length;
        }
        private void DisableAllControls()
        {
            randomize_Button.IsEnabled = false;
            save_Button.IsEnabled = false;
            load_Button.IsEnabled = false;
            start_Button.IsEnabled = false;
            clientCount_textBox.IsEnabled = false;
            min_textBox.IsEnabled = false;
            max_textBox.IsEnabled = false;
            percMaxTabu_textBox.IsEnabled = false;
            cap_textBox.IsEnabled = false;
            cadenceMod_textBox.IsEnabled = false;
            aspirationPlus_textBox.IsEnabled = false;
            aspirationPlusPlus_textBox.IsEnabled = false;
            delay_checkBox.IsEnabled = false;
        }
        private void EnableAllControls()
        {
            randomize_Button.IsEnabled = true;
            load_Button.IsEnabled = true;
            clientCount_textBox.IsEnabled = true;
            min_textBox.IsEnabled = true;
            max_textBox.IsEnabled = true;
            percMaxTabu_textBox.IsEnabled = true;
            cap_textBox.IsEnabled = true;
            cadenceMod_textBox.IsEnabled = true;
            aspirationPlus_textBox.IsEnabled = true;
            aspirationPlusPlus_textBox.IsEnabled = true;
            delay_checkBox.IsEnabled = true;
        }
        private void Randomize_Button_Click(object sender, RoutedEventArgs e)
        {
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
            UpdateParams();

            // *** losowanie pozycji klientów i rysowanie ich na mapie ***
            clients = new Point[clientsCount];
            clientsOrders = new int[clientsCount];
            for (int i = 0; i < clientsCount; i++)
            {
                clients[i] = new Point(rnd.Next(0, 1000), rnd.Next(0, 1000), i);
                clientsOrders[i] = rnd.Next(minMass, maxMass + 1);
            }
            DrawX.RemoveClients();
            DrawX.RemoveLines();
            DrawX.DrawClients(clients);
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
        }
        private void Save_Button_Click(object sender, RoutedEventArgs e)
        {
            string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(exePath, "plik1.txt")))
            {
                outputFile.WriteLine(clientsCount + ";" + minMass + ";" + maxMass + ";");
                for (int i = 0; i < clientsCount; i++)
                {
                    outputFile.WriteLine(clients[i].x + ";" + clients[i].y + ";" + clientsOrders[i] + ";");
                }
            }
            load_Button.IsEnabled = true;
        }
        private void Load_Button_Click(object sender, RoutedEventArgs e)
        {
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
            string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            using (StreamReader inputFile = new StreamReader(System.IO.Path.Combine(exePath, "plik1.txt")))
            {
                var lineA = inputFile.ReadLine();
                var valuesA = lineA.Split(';');
                if (!Int32.TryParse(valuesA[0], out clientsCount))
                    throw new Exception();
                clientCount_textBox.Text = clientsCount.ToString();
                DrawX.SendData(mainGrid, clientsCount);
                if (!Int32.TryParse(valuesA[1], out minMass))
                    throw new Exception();
                min_textBox.Text = minMass.ToString();
                if (!Int32.TryParse(valuesA[2], out maxMass))
                    throw new Exception();
                max_textBox.Text = maxMass.ToString();

                clients = new Point[clientsCount];
                clientsOrders = new int[clientsCount];

                int i = 0;
                while (!inputFile.EndOfStream)
                {
                    var line = inputFile.ReadLine();
                    var values = line.Split(';');
                    Double.TryParse(values[0], out double value0);
                    Double.TryParse(values[1], out double value1);
                    clients[i] = new Point(value0, value1);
                    Int32.TryParse(values[2], out int value2);
                    clientsOrders[i] = value2;
                    i++;
                }
            }
            DrawX.RemoveClients();
            DrawX.RemoveLines();
            DrawX.DrawClients(clients);
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
        }
        private void Load_Button2_Click(object sender, RoutedEventArgs e)
        {
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;

            OpenFileDialog okienko = new OpenFileDialog();
            okienko.ShowDialog();
            okienko.Filter = "|*.txt";
            string bufor = okienko.FileName;
            if (!bufor.Equals(""))
            {
                using (StreamReader inputFile = new StreamReader(@bufor))   // capacity->(linia_i=4,druga liczba), main baza - linia 9. koordynaty - druga i trzecia liczba
                {
                    int i = 0;
                    clientsCount = 0;
                    List<Point> clientsBuf = new List<Point>();
                    List<int> clientsOrdersBuf = new List<int>();
                    while (!inputFile.EndOfStream)
                    {
                        var lineA = inputFile.ReadLine();
                        var liniaTab = lineA.Split(' ');
                        if(i == 4)
                        {
                            int ii = 0;
                            for(int j = 0; j < liniaTab.Length; j++)
                            {
                                if (Int32.TryParse(liniaTab[j], out capacity))
                                {
                                    ii++;
                                    if (ii == 2)
                                    {
                                        cap_textBox.Dispatcher.Invoke(() => { cap_textBox.Text = capacity.ToString(); });
                                        break;
                                    }
                                }
                            }
                        }
                        else if (i == 9)
                        {
                            int ii = 0, xbuf = -1, ybuf = -1;
                            for (int j = 0; j < liniaTab.Length; j++)
                            {
                                if (Int32.TryParse(liniaTab[j], out ybuf))
                                {
                                    ii++;
                                    if (ii == 2)
                                    {
                                        xbuf = ybuf;
                                    }
                                    else if (ii == 3)
                                    {
                                        main_station = new Point(10 * xbuf, 10 * ybuf);
                                        DrawX.DrawMainStation(main_station);
                                        break;
                                    }
                                }
                            }
                        }
                        else if (i > 9)
                        {
                            int ii = 0, xbuf = -1, ybuf = -1;
                            for (int j = 0; j < liniaTab.Length; j++)
                            {
                                if (Int32.TryParse(liniaTab[j], out ybuf))
                                {
                                    ii++;
                                    if (ii == 2)
                                    {
                                        xbuf = ybuf;
                                    }
                                    else if (ii == 3)
                                    {
                                        clientsBuf.Add(new Point(10 * xbuf, 10 * ybuf, i - 10));
                                    }
                                    else if (ii == 4)
                                    {
                                        clientsOrdersBuf.Add(ybuf);
                                        break;
                                    }
                                }
                            }
                        }
                        i++;
                    }
                    if (debug) Console.WriteLine(capacity + ", " + main_station.x + ", " + main_station.y);
                    if (debug) Console.WriteLine(i);
                    clientsCount = clientsBuf.Count;
                    DrawX.SendData(mainGrid, clientsCount);
                    clients = new Point[clientsCount];
                    clientsOrders = new int[clientsCount];
                    for (int j = 0; j < clientsCount; j++)
                    {
                        if (debug) Console.WriteLine(clientsBuf[j].x + ", " + clientsBuf[j].y + ", " + clientsOrdersBuf[j]);
                        clients[j] = clientsBuf[j];
                        clientsOrders[j] = clientsOrdersBuf[j];
                    }
                    clientCount_textBox.Dispatcher.Invoke(() => { clientCount_textBox.Text = clientsCount.ToString(); });
                    DrawX.RemoveClients();
                    DrawX.DrawClients(clients);
                    start_Button.IsEnabled = true;
                }
            }
        }
        private void Start_Button_Click(object sender, RoutedEventArgs e)
        {
            DisableAllControls();
            Thread thread = new Thread(Start);
            thread.Start();
        }

        private void StartTest_Button_Click(object sender, RoutedEventArgs e)
        {
            DisableAllControls();
            Thread thread = new Thread(StartTest);
            thread.Start();
        }
        private void StartTest()
        {
            string exePath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(exePath, "Testy_parametrów.csv")))
            {
                outputFile.WriteLine("procent zakazanych ruchów [%];modyfikator kadencji (1-100);aspiracja plus;aspiracja plus plus;liczba iteracji;najlepszy koszt cyklu Hamiltona;najlepszy koszt");
            }
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                    for (int k = 0; k < 3; k++)
                        for (int l = 0; l < 3; l++)
                        {
                            //ustawianie kolejnego zestawu parametrów
                            percMaxTabu_textBox.Dispatcher.Invoke(() => { percMaxTabu_textBox.Text = percMaxTabu_T[i].ToString("F1"); });
                            cadenceMod_textBox.Dispatcher.Invoke(() => { cadenceMod_textBox.Text = cadenceMod_T[j].ToString(); });
                            aspirationPlus_textBox.Dispatcher.Invoke(() => { aspirationPlus_textBox.Text = aspirationPlus_T[k].ToString(); });
                            aspirationPlusPlus_textBox.Dispatcher.Invoke(() => { aspirationPlusPlus_textBox.Text = aspirationPlusPlus_T[l].ToString(); });

                            //start
                            Thread thread = new Thread(Start);
                            thread.Start();

                            //czekanie na zakończenie wątku
                            while (thread.IsAlive == true) { }

                            //zapis wyników danego zestawu do pliku
                            int buf1 = -1;
                            double buf2 = -1, buf3 = -1;
                            iteration_textBlock.Dispatcher.Invoke(() => { if (!Int32.TryParse(iteration_textBlock.Text, out buf1)) throw new Exception(); });
                            bestCost_textBlock.Dispatcher.Invoke(() => { if (!Double.TryParse(bestCost_textBlock.Text, out buf2)) throw new Exception(); });
                            finalCost_textBlock.Dispatcher.Invoke(() => { if (!Double.TryParse(finalCost_textBlock.Text, out buf3)) throw new Exception(); });
                            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(exePath, "Testy_parametrów.csv"), true))
                            {
                                outputFile.WriteLine(percMaxTabu_T[i].ToString("F1") + ";" + cadenceMod_T[j] + ";" + aspirationPlus_T[k] + ";" + aspirationPlusPlus_T[l] + ";" + buf1 + ";" + buf2 + ";" + buf3);
                            }
                        }
        }
        private void HamiltonCycleButton_Click(object sender, RoutedEventArgs e)
        {
            DrawX.RemoveLines();
            DrawX.DrawLines(bestTracks);
        }
        private void TracksButton_Click(object sender, RoutedEventArgs e)
        {
            DrawX.RemoveLines();
            DrawX.DrawLines(tracks);
        }
        private void EnableButtons()
        {
            randomize_Button.IsEnabled = true;
        }
        private void DisenableButtons()
        {
            start_Button.IsEnabled = false;
            randomize_Button.IsEnabled = false;
            save_Button.IsEnabled = false;
        }
        private void ClientCount_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void CadenceMod_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void PercMaxTabu_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void Cap_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void Min_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void Max_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void AspirationPlus_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void AspirationPlusPlus_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckAllTextBoxes();
        }
        private void CheckAllTextBoxes()
        {
            if (windowInited)
            {
                start_Button.IsEnabled = false;
                save_Button.IsEnabled = false;
                bool buf = true;
                if (Int32.TryParse(clientCount_textBox.Text, out int clientCount_buf) && clientCount_buf > 3)
                {
                    clientCount_textBox.Background = Brushes.LightGreen;
                }
                else
                {
                    clientCount_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (Int32.TryParse(cadenceMod_textBox.Text, out int cadenceMod_buf) && cadenceMod_buf >= 1 && cadenceMod_buf <= 100)
                {
                    cadenceMod_textBox.Background = Brushes.White;
                }
                else
                {
                    cadenceMod_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (Double.TryParse(percMaxTabu_textBox.Text, out double percMaxTabu_buf) && percMaxTabu_buf >= 0 && percMaxTabu_buf < 100)
                {
                    percMaxTabu_textBox.Background = Brushes.White;
                }
                else
                {
                    percMaxTabu_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (Int32.TryParse(cap_textBox.Text, out int cap_buf) && cap_buf > 0)
                {
                    cap_textBox.Background = Brushes.White;
                    min_textBox.IsEnabled = true;
                    max_textBox.IsEnabled = true;
                }
                else
                {
                    cap_textBox.Background = Brushes.Red;
                    buf = false;
                    min_textBox.IsEnabled = false;
                    max_textBox.IsEnabled = false;
                }

                Int32.TryParse(cap_textBox.Text, out int buf1);
                Int32.TryParse(max_textBox.Text, out int buf2);
                if (Int32.TryParse(min_textBox.Text, out int min_buf) && min_buf > 0 && min_buf <= buf1 && min_buf <= buf2)
                {
                    min_textBox.Background = Brushes.LightGreen;
                }
                else
                {
                    min_textBox.Background = Brushes.Red;
                    buf = false;
                }

                Int32.TryParse(cap_textBox.Text, out buf1);
                Int32.TryParse(min_textBox.Text, out buf2);
                if (Int32.TryParse(max_textBox.Text, out int max_buf) && max_buf > 0 && max_buf <= buf1 && max_buf >= buf2)
                {
                    max_textBox.Background = Brushes.LightGreen;
                }
                else
                {
                    max_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (Int32.TryParse(aspirationPlus_textBox.Text, out int aspirationPlus_buf) && aspirationPlus_buf > 0)
                {
                    aspirationPlus_textBox.Background = Brushes.White;
                }
                else
                {
                    aspirationPlus_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (Int32.TryParse(aspirationPlusPlus_textBox.Text, out int aspirationPlusPlus_buf) && aspirationPlusPlus_buf >= 0)
                {
                    aspirationPlusPlus_textBox.Background = Brushes.White;
                }
                else
                {
                    aspirationPlusPlus_textBox.Background = Brushes.Red;
                    buf = false;
                }

                if (buf == true)    //jesli nie ma błędu we wprowadzonych parametrach
                    EnableButtons();
                else    //jesli jest błąd we wprowadzonych parametrach
                    DisenableButtons();
            }
        }
    }
}

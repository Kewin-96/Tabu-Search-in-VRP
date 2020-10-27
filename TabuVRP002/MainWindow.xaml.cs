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
        //klient = punkt dostaw
        //główna baza - miejsce, z którego wyjeżdżają dostawczaki oraz do którego wracają

        //parametry
        public static int minMass = 3;
        public static int maxMass = 20;
        public static int clientsCount = 30;
        public static int capacity = 50; //pojemność dostawczaka
        public static int cadenceMod = 10; //modyfikator kadencji; kadencja = ilosc_klientów * (modyfikator_kadencji / 100)
        public static int cadence = (int)(clientsCount * (cadenceMod / 100.0));
        public static double percMaxTabu = 0.1; //procent zakazanych ruchów (stosunek maksymalnej ilości zakazanych ruchów do wszystkich możliwych ruchów)
        public static int aspirationPlus = 500;
        public static int aspirationPlusPlus = 5;
        public static double aspiration = 0.9; // jesli nastąpi poprawa (1-aspiration)*100 procent to zignorowac zakaz

        //wyniki parametrów
        public static int avaMoves = ((clientsCount - 2) * (clientsCount - 3)) / 2; //ilość możliwych ruchów
        public static int maxTabu = (int)((percMaxTabu * 0.01) * avaMoves); //maksymalna ilość zakazanych ruchów

        //Losowany zbiór danych wejściowych - zbiór klientów i masy zamówień
        public static Point[] clients; //zbiór klientów (wierzchołków)/ punktów dostaw
        public static int[] clientsOrders; //masy zamówień

        //Dane wyjściowe - trasy, cykl Hamiltona będzie ukryty po prostu pod tracks[0]
        public static List<List<Point>> tracks = new List<List<Point>>();   //trasy, każda trasa to zbiór wierzchołków - powinna zaczynać i kończyć się w bazie głównej, a za pokrewne punkty przyjmować kolejnych klientów/punkty dostaw
        public static List<List<Point>> bestTracks = new List<List<Point>>();
        public static double bestOfBestCost = 1e100;
        public static double finalCost = 1e100;

        //Lista Tabu
        public static List<int[]> tabuList = new List<int[]>();

        //inne
        Random rnd = new Random();
        Point main_station = new Point(500,500);
        bool windowInited = false;
        bool debug = false;

        //testy parametrow:
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
            DrawX.sendData(mainGrid, clientsCount);
            DrawX.drawMainStation(main_station);  //rysowanie głównej stacji
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
        private void start()
        {
            updateParams();

            int iteracjeBezPoprawy = aspirationPlusPlus;
            int iteracjaTotal = 1;
            bestOfBestCost = 1e100;
            finalCost = 1e100;
            iteracja_textBlock.Dispatcher.Invoke(() => { iteracja_textBlock.Text = "0"; });
            koszt_textBlock.Dispatcher.Invoke(() => { koszt_textBlock.Text = "0"; });
            najlepszyKoszt_textBlock.Dispatcher.Invoke(() => { najlepszyKoszt_textBlock.Text = "0"; });
            ostatecznyKoszt_textBlock.Dispatcher.Invoke(() => { ostatecznyKoszt_textBlock.Text = "0"; });
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
                            double bufD = lengthEuclid(tracks[0][i], bufCliets[j]);
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
                iteracja_textBlock.Dispatcher.Invoke(() => { iteracja_textBlock.Text = iteracjaTotal.ToString(); });
                double bestCost = calcHamiltonTrackLengh(tracks[0]);
                koszt_textBlock.Dispatcher.Invoke(() => { koszt_textBlock.Text = bestCost.ToString("F3"); });
                bool bufBreak = false, aspPlus = false;
                int iPlus = 0;
                bool paraTabu = false;

                // *** wyliczanie pierwszego kosztu ***
                koszt1_textBlock.Dispatcher.Invoke(() => { koszt1_textBlock.Text = bestCost.ToString("F3"); });

                // *** rysowanie wylosowanego cyklu Hamiltona ***
                DrawX.removeLines();
                DrawX.drawLines(tracks);

                // *** zmiana koloru kwadratu na jasny niebieski po poprzednich obliczeniach ; opóźnienie po losowaniu i przed rozpoczęciem algorytmu
                result_Rectangle.Dispatcher.Invoke(() => { result_Rectangle.Fill = new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0x71, 0xFF)); });
                //Thread.Sleep(500);

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
                            buf = calcHamiltonTrackNeighLengh(tracks[0], i, j);
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
                            //Console.WriteLine("Usuwanie elementu z tabu listy: (" + tabuList[i][0] + ", " + tabuList[i][1] + ")");
                            if (tabuList[i][2] == 0)
                                tabuList.RemoveAt(i);
                        }
                        if (tabuList.Count < maxTabu && bufworstCost > bufbestCost)    //jesli lista Tabu nie jest pełna i znalezione najogrsze rozwiązanie w iteracji jest gorsze od najlepszego rozwiązania
                        {
                            int[] bufP = { bufworstIndex1, bufworstIndex2, cadence };
                            tabuList.Add(bufP);
                            //Console.WriteLine("Dodano element do tabu listy: (" + bufP[0] + ", " + bufP[1] + ")");
                        }
                    }

                    // *** rysowanie cyklu Hamiltona ***
                    DrawX.removeLines();
                    DrawX.drawLines(tracks);

                    // *** wyswietlanie kosztu ***
                    koszt_textBlock.Dispatcher.Invoke(() => { koszt_textBlock.Text = bestCost.ToString("F3"); });

                    // *** iteracja ++ ***
                    iteracja++;
                    iteracjaTotal++;
                    iteracja_textBlock.Dispatcher.Invoke(() => { iteracja_textBlock.Text = iteracjaTotal.ToString(); });

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
                        { if (debug) Console.Write(calcHamiltonTrackLengh(bestTracks[0]) + " != "); }
                    else if (bestTracks.Count != 0)
                        throw new Exception("Bardzo zle: " + bestTracks.Count);
                    bestOfBestCost = bestCost;
                    najlepszyKoszt_textBlock.Dispatcher.Invoke(() => { najlepszyKoszt_textBlock.Text = bestOfBestCost.ToString("F3"); });
                    bestTracks = new List<List<Point>>();
                    bestTracks.Add(new List<Point>());
                    for (int i = 0; i < tracks[0].Count; i++)
                        bestTracks[0].Add(tracks[0][i]);
                    if (bestTracks.Count == 1)
                        { if (debug) Console.WriteLine(calcHamiltonTrackLengh(bestTracks[0])); }
                }
                else
                {
                    iteracjeBezPoprawy--;
                }
            }

            // *** wyswietlenie najlepszego cyklu Hamiltona ***
            DrawX.removeLines();
            DrawX.drawLines(bestTracks);

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
                        else if (clientsOrders[bestTracks[0][jj].i] < bufCap)   //indeks poza EXCEPTION
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
                double buf = calcTracksLengh(bufTracks);
                if (bufBestCost > buf)
                {
                    //Console.WriteLine("********************");
                    bufBestCost = buf;
                    tracks = new List<List<Point>>();
                    for (int j = 0; j < bufTracks.Count; j++)
                    {
                        tracks.Add(new List<Point>());
                        for (int k = 0; k < bufTracks[j].Count; k++)
                        {
                            tracks[j].Add(bufTracks[j][k]);
                            //Console.WriteLine(bufTracks[j][k].i);
                        }
                    }
                    //Console.WriteLine(tracks.Count);
                    //Console.WriteLine("********************");
                }
            }

            // tu sprawdzic czy masy się zgadzają (wyliczyc mase kazdej trasy i wyswietlac sprawdzanie)
            int buf001 = 0, buf002 = 0;
            for (int i = 0; i < clientsOrders.Length; i++)
            {
                buf001 += clientsOrders[i];
            }
            for (int i = 0; i < tracks.Count; i++)  // jest błąd - ten kawalek algorytmu dodaje 2 razy klientów, którzy są odwiedzani 2 razy
            {
                //Console.Write("i= " + i + ", j = ");
                for (int j = 1; j < tracks[i].Count-1; j++)
                {
                    //Console.Write(j + ", ");
                    if (i > 0)
                    {
                        //Console.WriteLine("XXX "+ tracks[i - 1][tracks[i - 1].Count - 2].i + " " + tracks[i][j].i + " XXX");
                        if (j == 1 && tracks[i - 1][tracks[i - 1].Count - 2].i != tracks[i][j].i)
                        {
                            buf002 += clientsOrders[tracks[i][j].i];
                            //Console.WriteLine("test1");
                        }
                        else if (j > 1)
                        {
                            buf002 += clientsOrders[tracks[i][j].i];
                            //Console.WriteLine("test2");
                        }
                    }
                    else
                    {
                        buf002 += clientsOrders[tracks[i][j].i];
                        //Console.WriteLine("test3");
                    }
                }
                //Console.WriteLine();
            }
            /*if (buf001 == buf002)
                Console.WriteLine("Dobrze: " + buf001 + " = " + buf002);*/
            if (buf001 != buf002)
                { if (debug) Console.WriteLine(" UWAGA BŁĄD: " + buf001 + " != " + buf002); }

            // *** wyswietlenie ostatecznego kosztu ***
            finalCost = calcTracksLengh(tracks);
            ostatecznyKoszt_textBlock.Dispatcher.Invoke(() => { ostatecznyKoszt_textBlock.Text = finalCost.ToString("F3"); });

            // *** rysowanie ostateczne ***
            DrawX.removeLines();
            DrawX.drawLines(tracks);

            // *** kwadrat na zielono ***
            result_Rectangle.Dispatcher.Invoke(() => { result_Rectangle.Fill = Brushes.Green; });

            // *** Aktywacja wszystkich kontrolek ***
            mainGrid.Dispatcher.Invoke(() =>
            {
                enableAllControls();
                start_Button.IsEnabled = true;
            });
        }
        private void updateParams()
        {
            clientCount_textBox.Dispatcher.Invoke(() => { if (!Int32.TryParse(clientCount_textBox.Text, out clientsCount)) throw new Exception(); });
            avaMoves = ((clientsCount - 2) * (clientsCount - 3)) / 2;
            DrawX.sendData(mainGrid, clientsCount);
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
        private double lengthEuclid(Point a, Point b)
        {
            return Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));
        }
        private double calcHamiltonTrackLengh(List<Point> graph)
        {
            double length = 0;
            for (int i = 1; i < graph.Count; i++)
            {
                length += lengthEuclid(graph[i - 1], graph[i]);
            }
            return length;
        }
        private double calcTracksLengh(List<List<Point>> graph)
        {
            double length = 0;
            for (int i = 0; i < graph.Count; i++)
            {
                length += calcHamiltonTrackLengh(graph[i]);
            }
            return length;
        }
        private double calcHamiltonTrackNeighLengh(List<Point> graph, int index1, int index2)
        {
            double length = 0;
            for (int i = 0; i < graph.Count-1; i++)
            {
                if ((index1 == (index2 - 1)) && i == index1)
                {
                    length += lengthEuclid(graph[index2], graph[index1]);
                }
                else if (i == index1 - 1)
                {
                    length += lengthEuclid(graph[i], graph[index2]);
                }
                else if (i == index1)
                {
                    length += lengthEuclid(graph[index2], graph[i + 1]);
                }
                else if (i == index2 - 1)
                {
                    length += lengthEuclid(graph[i], graph[index1]);
                }
                else if (i == index2)
                {
                    length += lengthEuclid(graph[index1], graph[i + 1]);
                }
                else
                    length += lengthEuclid(graph[i], graph[i+1]);
            }
            return length;
        }
        private void disableAllControls()
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
        private void enableAllControls()
        {
            randomize_Button.IsEnabled = true;
            //save_Button.IsEnabled = true;
            load_Button.IsEnabled = true;
            //start_Button.IsEnabled = true;
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
        private void randomize_Button_Click(object sender, RoutedEventArgs e)
        {
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
            updateParams();
            // *** losowanie pozycji klientów i rysowanie ich na mapie ***
            clients = new Point[clientsCount];
            clientsOrders = new int[clientsCount];
            for (int i = 0; i < clientsCount; i++)
            {
                clients[i] = new Point(rnd.Next(0, 1000), rnd.Next(0, 1000), i);
                clientsOrders[i] = rnd.Next(minMass, maxMass + 1);
            }
            DrawX.removeClients();
            DrawX.removeLines();
            DrawX.drawClients(clients);
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
        }
        private void save_Button_Click(object sender, RoutedEventArgs e)
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
        private void load_Button_Click(object sender, RoutedEventArgs e)
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
                DrawX.sendData(mainGrid, clientsCount);
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
            DrawX.removeClients();
            DrawX.removeLines();
            DrawX.drawClients(clients);
            start_Button.IsEnabled = true;
            save_Button.IsEnabled = true;
        }
        private void load_Button2_Click(object sender, RoutedEventArgs e)
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
                                        DrawX.drawMainStation(main_station);
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
                    DrawX.sendData(mainGrid, clientsCount);
                    clients = new Point[clientsCount];
                    clientsOrders = new int[clientsCount];
                    for (int j = 0; j < clientsCount; j++)
                    {
                        if (debug) Console.WriteLine(clientsBuf[j].x + ", " + clientsBuf[j].y + ", " + clientsOrdersBuf[j]);
                        clients[j] = clientsBuf[j];
                        clientsOrders[j] = clientsOrdersBuf[j];
                    }
                    clientCount_textBox.Dispatcher.Invoke(() => { clientCount_textBox.Text = clientsCount.ToString(); });
                    DrawX.removeClients();
                    DrawX.drawClients(clients);
                    start_Button.IsEnabled = true;
                }
            }
        }
        private void start_Button_Click(object sender, RoutedEventArgs e)
        {
            disableAllControls();
            Thread thread = new Thread(start);
            thread.Start();
        }

        private void startTest_Button_Click(object sender, RoutedEventArgs e)
        {
            disableAllControls();
            Thread thread = new Thread(startTest);
            thread.Start();
        }
        private void startTest()
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
                        {//percMaxTabu_T,cadenceMod_T,aspirationPlus_T,aspirationPlusPlus_T
                            //ustawianie kolejnego zestawu parametrów
                            percMaxTabu_textBox.Dispatcher.Invoke(() => { percMaxTabu_textBox.Text = percMaxTabu_T[i].ToString("F1"); });
                            cadenceMod_textBox.Dispatcher.Invoke(() => { cadenceMod_textBox.Text = cadenceMod_T[j].ToString(); });
                            aspirationPlus_textBox.Dispatcher.Invoke(() => { aspirationPlus_textBox.Text = aspirationPlus_T[k].ToString(); });
                            aspirationPlusPlus_textBox.Dispatcher.Invoke(() => { aspirationPlusPlus_textBox.Text = aspirationPlusPlus_T[l].ToString(); });

                            //start
                            Thread thread = new Thread(start);
                            thread.Start();

                            //czekanie na zakończenie wątku
                            while (thread.IsAlive == true) { }

                            //zapis wyników danego zestawu do pliku
                            int buf1 = -1;
                            double buf2 = -1, buf3 = -1;
                            iteracja_textBlock.Dispatcher.Invoke(() => { if (!Int32.TryParse(iteracja_textBlock.Text, out buf1)) throw new Exception(); });
                            najlepszyKoszt_textBlock.Dispatcher.Invoke(() => { if (!Double.TryParse(najlepszyKoszt_textBlock.Text, out buf2)) throw new Exception(); });
                            ostatecznyKoszt_textBlock.Dispatcher.Invoke(() => { if (!Double.TryParse(ostatecznyKoszt_textBlock.Text, out buf3)) throw new Exception(); });
                            using (StreamWriter outputFile = new StreamWriter(System.IO.Path.Combine(exePath, "Testy_parametrów.csv"), true))
                            {
                                outputFile.WriteLine(percMaxTabu_T[i].ToString("F1") + ";" + cadenceMod_T[j] + ";" + aspirationPlus_T[k] + ";" + aspirationPlusPlus_T[l] + ";" + buf1 + ";" + buf2 + ";" + buf3);
                            }
                        }
        }
        private void cyklHamiltonaButton_Click(object sender, RoutedEventArgs e)
        {
            DrawX.removeLines();
            DrawX.drawLines(bestTracks);
        }
        private void trasyButton_Click(object sender, RoutedEventArgs e)
        {
            DrawX.removeLines();
            DrawX.drawLines(tracks);
        }
        private void enableButtons()
        {
            //start_Button.IsEnabled = true;
            randomize_Button.IsEnabled = true;
            //save_Button.IsEnabled = true;
        }
        private void disenableButtons()
        {
            start_Button.IsEnabled = false;
            randomize_Button.IsEnabled = false;
            save_Button.IsEnabled = false;
        }
        private void clientCount_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void cadenceMod_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void percMaxTabu_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void cap_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void min_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void max_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void aspirationPlus_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void aspirationPlusPlus_textBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            checkAllTextBoxes();
        }
        private void checkAllTextBoxes()
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
                    enableButtons();
                else    //jesli jest błąd we wprowadzonych parametrach
                    disenableButtons();
            }
        }
    }
}

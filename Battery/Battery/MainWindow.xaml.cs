﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SqlClient;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System.Power;
using System.Threading;
using System.Data;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Battery
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }

        public static int i = 0;
        public static bool run = true;

        public void RunApplicationAsync(bool start)
        {
            String batteryStatus = PowerManager.BatteryStatus.ToString();
            int remainingCharge = PowerManager.RemainingChargePercent;
            string time = DateTime.Now.ToString("HH:mm:ss");
            Insert(remainingCharge, time, batteryStatus);

            run = start;

            bool charge = batteryStatus.Equals("Charging");

            bool cent = remainingCharge==100;
            
            while (run)
            {
                remainingCharge = PowerManager.RemainingChargePercent;
                time = DateTime.Now.ToString("HH:mm:ss");
                batteryStatus = PowerManager.BatteryStatus.ToString();
                if (cent && remainingCharge == 99)
                {
                    cent = false;
                }

                if (batteryStatus.Equals("Charging") && !charge)
                {
                    charge = true;
                    Insert(remainingCharge, time, batteryStatus);
                }
                else if (batteryStatus.Equals("Discharging") && charge)
                {
                    charge = false;
                    Insert(remainingCharge, time, batteryStatus);
                }
                else if (!cent && remainingCharge == 100)
                {
                    cent = true;
                    Insert(remainingCharge, time, batteryStatus);
                }
                else if (time.Contains(":00:00"))
                {
                    Insert(remainingCharge, time, batteryStatus);
                    Thread.Sleep(1000);
                }

                /*PowerManager.BatteryStatusChanged += (s,args) =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        this.Debug.Text = pwr.ToString();
                        i += 1;
                    });
                };*/
            }
            return;
        }
        public void Insert(int remainingCharge, String time, String batteryStatus)
        {
            string connection = @"Server=INL393\SQLEXPRESS; database=battery; trusted_connection=yes";
            SqlConnection conn = new SqlConnection(connection);
            conn.Open();
            SqlCommand comm = new SqlCommand();
            comm.Connection = conn;
            comm.CommandText = "insert into charge values(" + remainingCharge +",'" + time + "','" + batteryStatus + "')";
            SqlDataReader dr = comm.ExecuteReader();
            conn.Close();

            /*Console.WriteLine("Battery Percent: " + remainingCharge);
            Console.WriteLine(time);
            Console.WriteLine("battery charge status : " + strBatteryChargingStatus);*/
        }
        /*public class Event
        {
            public EventHandler ChangeEvent;
            public void OnChange()
            {
                ChangeEvent.Invoke(this, EventArgs.Empty);
            }
        }*/

        public Report GetReport()
        {
            string connection = @"Server=INL393\SQLEXPRESS; database=battery; trusted_connection=yes";
            SqlConnection conn = new SqlConnection(connection);
            conn.Open();
            SqlCommand comm = new SqlCommand();
            comm.Connection = conn;
            comm.CommandText = "Select * from charge";
            SqlDataReader dr = comm.ExecuteReader();
            DataTable dt = new DataTable();
            dt.Load(dr);
            List<DataRow> list = dt.AsEnumerable().ToList();

            List<String> hourReport = new List<string>();
            string centStart = "";
            int badCount = 0, optimalCount = 0, spotCount = 0;


            int previousChargePercent = 0, percent;
            DateTime previousTime = new DateTime(), time;
            string previousChargeStatus = "", chargeStatus;

            double dischargeTime = 0;
            int dischargePercent = 0;


            for (int i = 0; i < list.Count; i++)
            {
                if (i == 0)
                {
                    previousChargePercent = (int)list[i]["percentage"];
                    previousTime = DateTime.Parse(list[i]["time"].ToString());
                    previousChargeStatus = (string)list[i]["charge_status"];

                    bool cent = previousChargePercent == 100;
                    bool charge = previousChargeStatus.Equals("Charging");
                    continue;
                }
                percent = (int)list[i]["percentage"];
                time = DateTime.Parse(list[i]["time"].ToString());
                chargeStatus = (string)list[i]["charge_status"];

                if (centStart == "" && percent == 100 && chargeStatus.Equals("Charging"))
                {
                    centStart = time.ToString();
                }
                else if (percent == 100 && (chargeStatus.Equals("Discharging") || (chargeStatus.Equals("Charging") && (i == list.Count - 1))))
                {
                    TimeSpan ts = (time - DateTime.Parse(centStart));
                    /*Console.WriteLine(time.ToString() + "   " + centStart + "   " + ts.ToString());*/
                    if (ts.Hours > 0 || ts.Minutes >= 30)
                        badCount += 1;
                    else
                        optimalCount += 1;
                    centStart = "";
                }
                else if (chargeStatus.Equals("Discharging") && previousChargeStatus.Equals("Charging") && percent < 100)
                {
                    spotCount += 1;
                }

                if (chargeStatus.Equals("Charging") && previousChargeStatus.Equals("Discharging"))
                {
                    dischargeTime += time.Subtract(previousTime).TotalMinutes;
                    dischargePercent += previousChargePercent - percent;
                }
                if (time.ToString().Contains(":00:00") || (i == list.Count - 1))
                {
                    if (chargeStatus.Equals("Discharging") && previousChargeStatus.Equals("Discharging"))
                    {
                        if (time.ToString().Contains(":00:00"))
                            dischargeTime += 60 - previousTime.Minute;
                        else
                            dischargeTime += time.Subtract(previousTime).TotalMinutes;
                        dischargePercent += previousChargePercent - percent;
                    }
                    String curReport = dischargePercent + "% discharged in " + dischargeTime + " minutes";
                    hourReport.Add(curReport);
                    dischargeTime = 0;
                    dischargePercent = 0;
                }
                previousChargePercent = percent;
                previousTime = time;
                previousChargeStatus = chargeStatus;

                /*Console.WriteLine(percent);
                Console.WriteLine(time);
                Console.WriteLine(chargeStatus);
                Console.WriteLine();*/
            }
            conn.Close();

            string hourly = String.Join(" \n ", hourReport);
            string centCount = "Bad:" + badCount + " Optimal:" + optimalCount + " Spot:" + spotCount;
            Report final = new Report();
            final.HourlyReport = hourly;
            final.CentReport = centCount;

            return final;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            startButton.Content = "Started";
            await Task.Run(() => RunApplicationAsync(true));

            /*String batteryStatus = PowerManager.BatteryStatus.ToString();
            bool chargeStatus = batteryStatus.Equals("Charging");
            String powerStatus = PowerManager.PowerSupplyStatus.ToString();
            int remainingCharge = PowerManager.RemainingChargePercent;
            this.Debug.Text = batteryStatus + "  " + chargeStatus + "  " + powerStatus + "  " + remainingCharge;*/

        }
        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            RunApplicationAsync(false);
            stopButton.Content = "Stopped";
            Report result = GetReport();


            this.result.Text = result.HourlyReport;
            this.Debug.Text = result.CentReport;
        }
    }

    public class Report
    {
        public String HourlyReport { get; set; }
        public String CentReport { get; set; }
    }
}

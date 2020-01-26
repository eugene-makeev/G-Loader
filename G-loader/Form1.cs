﻿using System;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace G_loader
{
    public partial class Form1 : Form
    {
        SerialPort port = new SerialPort();
        string filename;
        int percents = 0;
        UInt16 startAddress = 0;
        UInt16 maxFileSize = 0;

        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            port.BaudRate = 115200;
            port.StopBits = StopBits.One;
            port.DataBits = 8;
            port.ReadTimeout = 500;
            port.WriteTimeout = 50;
            port.NewLine = "]";

            comboBox1.Items.Add("Autodetect");
            comboBox1.SelectedIndex = 0;

            // populate list of com-ports
            string[] enableComPorts = SerialPort.GetPortNames();
            foreach (string port in enableComPorts)
            {
                comboBox1.Items.Add(port);
            }
        }

        public void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (port.IsOpen)
            {
                portWrite("[R]");
                port.Close();
            }
        }

        private bool portOpen(string portName)
        {
            port.PortName = portName;
            byte retry = 3;

            try
            {
                port.Open();

                while (retry !=0)
                {
                    portWrite("[I]");
                    string response = port.ReadLine();

                    if (response.Contains("MyGrbl"))
                    {
                        return port.IsOpen;
                    }

                    retry--;
                }

                port.Close();
            }
            catch
            {
                portWrite("[R]");
                port.Close();
            }

            return port.IsOpen;
        }

        private int portWrite(string str)
        {
            if (port.IsOpen)
            {
                try
                {
                    port.Write(str);
                }
                catch
                {
                    return 1;
                }
            }

            return 0;
        }

        private int portWrite(byte[] data)
        {
            if (port.IsOpen)
            {
                try
                {
                    port.Write(data, 0, data.Length);
                }
                catch
                {
                    return 1;
                }
            }

            return 0;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Connect.Text == "Подключить")
            {
                Status.Text = "Подключение ...";
                comboBox1.Enabled = false;
                int index = comboBox1.SelectedIndex;

                if (index == 0)
                {
                    comboBox1.Items.Clear();
                    comboBox1.Items.Add("Autodetect");
                    comboBox1.SelectedIndex = 0;
                    // populate list of com-ports
                    string[] enableComPorts = SerialPort.GetPortNames();
                    foreach (string port in enableComPorts)
                    {
                        comboBox1.Items.Add(port);
                    }
                    // autodetect
                    while (++index < comboBox1.Items.Count)
                    {
                        comboBox1.SelectedIndex = index;
                        if (portOpen(comboBox1.GetItemText(comboBox1.SelectedItem)))
                        {
                            break;
                        }
                    }
                }
                else if (index > 0)
                {
                    portOpen(comboBox1.GetItemText(comboBox1.SelectedItem));
                }

                if (!port.IsOpen)
                {
                    string message = "Устройство не найдено или порт уже\nиспользуется другой программой";
                    string title = "Ошибка подключения";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    MessageBox.Show(message, title, buttons, MessageBoxIcon.Warning);
                }
            }
            else
            {
                portWrite("[R]");
                port.Close();
            }

            Connect.Text = port.IsOpen ? "Отключить" : "Подключить";
            Status.Text = port.IsOpen ? "Подключен" : "Отключен";
            FwLoad.Enabled = GcodeLoad.Enabled = port.IsOpen;
            comboBox1.SelectedIndex = port.IsOpen ? comboBox1.SelectedIndex : 0;
            comboBox1.Enabled = !port.IsOpen;
        }

        private void SendFileThread()
        {
            byte[] fileData = File.ReadAllBytes(filename);
            UInt16 pageSize = 128;
            int fileLength = fileData.Length;
            UInt16 start = startAddress;

            byte errorCode = 0;

            if (fileLength > maxFileSize)
            {
                errorCode = 1;
            }
            else
            {
                for (UInt16 offset = 0; offset < fileLength;)
                {
                    UInt16 remainingBytes = (UInt16)(fileLength - offset);
                    UInt16 bytesToSend = remainingBytes > pageSize ? pageSize : remainingBytes;
                    byte retries = 0;

                    try
                    {
                        if (portWrite("[P]") == 0)
                        {
                            Thread.Sleep(5);
                            // need synchronisation here?
                            byte[] length = BitConverter.GetBytes(bytesToSend + sizeof(UInt16));
                            byte[] address = BitConverter.GetBytes(start + offset);
                            byte[] data = fileData.Skip(offset).Take(bytesToSend).ToArray();
                            byte[] payload = address.Take(2).Concat(data).ToArray();
                            byte[] crc16 = BitConverter.GetBytes(CRC.CRC16(payload, payload.Length));
                            byte[] packet = length.Take(1).Concat(payload).Concat(crc16).ToArray();

                            if (portWrite(packet) == 0)
                            {
                                if (port.ReadLine().Contains("nak"))
                                {
                                    if (++retries > 5)
                                    {
                                        errorCode = 2;
                                        break;
                                    }
                                    continue;
                                }
                            }
                        }
                    }
                    catch
                    {
                        errorCode = 3;
                        break;
                    }

                    offset += bytesToSend;
                    percents = (offset * 100 / fileLength);

                    Status.Invoke((Action)delegate 
                    {
                        Status.Text = "Отправка " + percents.ToString() + "%";
                    });
                }
            
            }

            if (errorCode != 0)
            {
                string message;

                switch (errorCode)
                {
                    case 1:
                        message = "Размер файла превышает максимально допустимый: " + (maxFileSize / 1024).ToString() + "кБ";
                        break;
                    case 2:
                        message = "Достигнуто максимальное количество повторений при передаче данных";
                        break;
                    default:
                        message = "Непредвиденная ошибка, перезапустите устройство и программу";
                        break;
                }

                string title = "Ошибка обновления";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show(message, title, buttons, MessageBoxIcon.Warning);
            }
            else
            {
                string title = "Готово";
                MessageBoxButtons buttons = MessageBoxButtons.OK;
                MessageBox.Show("Загрузка файла завершена", title, buttons, MessageBoxIcon.Information);
            }
        }

        private DialogResult openAndSendFile(string filter)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = filter;

            DialogResult result = openDialog.ShowDialog();

            if (result != DialogResult.Cancel)
            {
                filename = openDialog.FileName;

                // send file over COM port
                Thread t = new Thread(new ThreadStart(SendFileThread));
                t.Start();

                while (t.IsAlive)
                {
                    Application.DoEvents();
                }

                Status.Text = port.IsOpen ? "Подключен" : "Отключен";
            }

            return result;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            startAddress = 0x6800;
            maxFileSize = 2 * 1024;
            if (openAndSendFile("All files(*.*)|*.*") != DialogResult.Cancel)
            {

            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            startAddress = 0;
            maxFileSize = 28 * 1024;
            if (openAndSendFile("Bin files(*.bin)|*.bin") != DialogResult.Cancel)
            {

            }
        }

        private void comboBox1_DropDown(object sender, EventArgs e)
        {
            comboBox1.Items.Clear();
            comboBox1.Items.Add("Autodetect");
            comboBox1.SelectedIndex = 0;
            // populate list of com-ports
            string[] enableComPorts = SerialPort.GetPortNames();
            foreach (string port in enableComPorts)
            {
                comboBox1.Items.Add(port);
            }
        }
    }

    public static class CRC
    {
        static readonly ushort[] crc16Table = new ushort[]
        {
        0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50A5, 0x60C6, 0x70E7,
        0x8108, 0x9129, 0xA14A, 0xB16B, 0xC18C, 0xD1AD, 0xE1CE, 0xF1EF,
        0x1231, 0x0210, 0x3273, 0x2252, 0x52B5, 0x4294, 0x72F7, 0x62D6,
        0x9339, 0x8318, 0xB37B, 0xA35A, 0xD3BD, 0xC39C, 0xF3FF, 0xE3DE,
        0x2462, 0x3443, 0x0420, 0x1401, 0x64E6, 0x74C7, 0x44A4, 0x5485,
        0xA56A, 0xB54B, 0x8528, 0x9509, 0xE5EE, 0xF5CF, 0xC5AC, 0xD58D,
        0x3653, 0x2672, 0x1611, 0x0630, 0x76D7, 0x66F6, 0x5695, 0x46B4,
        0xB75B, 0xA77A, 0x9719, 0x8738, 0xF7DF, 0xE7FE, 0xD79D, 0xC7BC,
        0x48C4, 0x58E5, 0x6886, 0x78A7, 0x0840, 0x1861, 0x2802, 0x3823,
        0xC9CC, 0xD9ED, 0xE98E, 0xF9AF, 0x8948, 0x9969, 0xA90A, 0xB92B,
        0x5AF5, 0x4AD4, 0x7AB7, 0x6A96, 0x1A71, 0x0A50, 0x3A33, 0x2A12,
        0xDBFD, 0xCBDC, 0xFBBF, 0xEB9E, 0x9B79, 0x8B58, 0xBB3B, 0xAB1A,
        0x6CA6, 0x7C87, 0x4CE4, 0x5CC5, 0x2C22, 0x3C03, 0x0C60, 0x1C41,
        0xEDAE, 0xFD8F, 0xCDEC, 0xDDCD, 0xAD2A, 0xBD0B, 0x8D68, 0x9D49,
        0x7E97, 0x6EB6, 0x5ED5, 0x4EF4, 0x3E13, 0x2E32, 0x1E51, 0x0E70,
        0xFF9F, 0xEFBE, 0xDFDD, 0xCFFC, 0xBF1B, 0xAF3A, 0x9F59, 0x8F78,
        0x9188, 0x81A9, 0xB1CA, 0xA1EB, 0xD10C, 0xC12D, 0xF14E, 0xE16F,
        0x1080, 0x00A1, 0x30C2, 0x20E3, 0x5004, 0x4025, 0x7046, 0x6067,
        0x83B9, 0x9398, 0xA3FB, 0xB3DA, 0xC33D, 0xD31C, 0xE37F, 0xF35E,
        0x02B1, 0x1290, 0x22F3, 0x32D2, 0x4235, 0x5214, 0x6277, 0x7256,
        0xB5EA, 0xA5CB, 0x95A8, 0x8589, 0xF56E, 0xE54F, 0xD52C, 0xC50D,
        0x34E2, 0x24C3, 0x14A0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
        0xA7DB, 0xB7FA, 0x8799, 0x97B8, 0xE75F, 0xF77E, 0xC71D, 0xD73C,
        0x26D3, 0x36F2, 0x0691, 0x16B0, 0x6657, 0x7676, 0x4615, 0x5634,
        0xD94C, 0xC96D, 0xF90E, 0xE92F, 0x99C8, 0x89E9, 0xB98A, 0xA9AB,
        0x5844, 0x4865, 0x7806, 0x6827, 0x18C0, 0x08E1, 0x3882, 0x28A3,
        0xCB7D, 0xDB5C, 0xEB3F, 0xFB1E, 0x8BF9, 0x9BD8, 0xABBB, 0xBB9A,
        0x4A75, 0x5A54, 0x6A37, 0x7A16, 0x0AF1, 0x1AD0, 0x2AB3, 0x3A92,
        0xFD2E, 0xED0F, 0xDD6C, 0xCD4D, 0xBDAA, 0xAD8B, 0x9DE8, 0x8DC9,
        0x7C26, 0x6C07, 0x5C64, 0x4C45, 0x3CA2, 0x2C83, 0x1CE0, 0x0CC1,
        0xEF1F, 0xFF3E, 0xCF5D, 0xDF7C, 0xAF9B, 0xBFBA, 0x8FD9, 0x9FF8,
        0x6E17, 0x7E36, 0x4E55, 0x5E74, 0x2E93, 0x3EB2, 0x0ED1, 0x1EF0
        };

        public static ushort CRC16(byte[] bytes, int len)
        {
            ushort crc = 0xFFFF;
            for (var i = 0; i < len; i++)
                crc = (ushort)((crc << 8) ^ crc16Table[(crc >> 8) ^ bytes[i]]);
            return crc;
        }
    }
}

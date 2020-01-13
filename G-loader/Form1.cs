using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace G_loader
{
    public partial class Form1 : Form
    {
        SerialPort port = new SerialPort();
        string filename;
        string fileText;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // populate list of com-ports
            string[] enableComPorts = SerialPort.GetPortNames();
            foreach (string port in enableComPorts)
            {
                comboBox1.Items.Add(port);
            }

            if (comboBox1.Items.Count != 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (Connect.Text == "Подключить")
            {
                port.PortName = comboBox1.GetItemText(comboBox1.SelectedItem);
                port.BaudRate = 115200;
                port.Parity = Parity.None;
                port.StopBits = StopBits.One;
                port.DataBits = 8;
                port.Handshake = Handshake.None;
                port.RtsEnable = false;
                try
                {
                    port.Open();
                    Connect.Text = "Отключить";
                    comboBox1.Enabled = false;
                    FwLoad.Enabled = GcodeLoad.Enabled = true;
                    Status.Text = "Подключен";
                }
                catch
                {
                    string message = "Порт уже используется другой программой";
                    string title = "Ошибка подключения";
                    MessageBoxButtons buttons = MessageBoxButtons.OK;
                    MessageBox.Show(message, title, buttons, MessageBoxIcon.Warning);
                }
            }
            else
            {
                Connect.Text = "Подключить";
                comboBox1.Enabled = true;
                FwLoad.Enabled = GcodeLoad.Enabled = false;
                port.Close();
                Status.Text = "Отключен";
            }
        }

        private void SendFileThread()
        {
            Status.Text = "Отправка 0%";
            int fileLength = fileText.Length;
            int blockSize = 128;
            int percentsOfBlock = fileLength / blockSize;
            while (fileLength != 0)
            {
                fileLength--;
                Status.Text = "Отправка %";
            }
        }

        private void ResponseThread()
        {

        }

        private DialogResult openAndSendFile(string filter)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = filter;

            DialogResult result = openDialog.ShowDialog();

            if (result != DialogResult.Cancel)
            {
                filename = openDialog.FileName;
                fileText = System.IO.File.ReadAllText(filename);

                // send file over COM port
                Thread t = new Thread(new ThreadStart(SendFileThread));
                t.Start();
                t.Join();
            }

            return result;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (openAndSendFile("All files(*.*)|*.*") != DialogResult.Cancel)
            {

            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (openAndSendFile("Bin files(*.bin)|*.bin") != DialogResult.Cancel)
            {

            }
        }

    }
}

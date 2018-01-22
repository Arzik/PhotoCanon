using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using EOSDigital.API;
using System.IO;
using EOSDigital.SDK;
using System.Threading;

namespace PhotoCanon
{
    public partial class Form1 : Form
    {
        CanonAPI APIHandler; //объявление API Canon
        Camera MainCamera; //объявление переменной для камеры
        //CameraValue[] AvList; //объявление переменной для настройки глубины резкости
        //CameraValue[] TvList; //объявление переменной для настройки выдержки фотоаппарата 
        //CameraValue[] ISOList; //объявление переменной для настройки ISO фотоаппарата
        List<Camera> CamList; //объявление списка камер 
        bool IsInit = false;
        Bitmap Evf_Bmp; //объявление переменной для фото
        int LVBw, LVBh, w, h; //объявление переменных размеров
        float LVBratio, LVration;

        int TimePhoto;
        int CountPhoto;
        string[] Flines = new string[4];
        bool SeriesOn = false;

        int ErrCount; //объявление переменной для подсчета ошибок 
        object ErrLock = new object(); //объвление синхронизирующего объкта для ошибок
        object LvLock = new object(); //обявление синхронизирующего объкта для кнопок

        List<string> ImageArray = new List<string>();
        int i;


        public Form1()
        {
            InitializeComponent(); //при инициализации формы
            APIHandler = new CanonAPI(); //полное объявление API Canon
            APIHandler.CameraAdded += APIHandler_CameraAdded;
            //ErrorHandler.SevereErrorHappened += ErrorHandler_SevereErrorHappened;
            //ErrorHandler.NonSevereErrorHappened += ErrorHandler_NonSevereErrorHappened;
            /*SavePathTextBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "RemotePhoto");
            SaveFolderBrowser.Description = "Save Images To...";*/
            pictureBox1.Paint += pictureBox1_Paint;
            LVBw = pictureBox1.Width;
            LVBh = pictureBox1.Height;
            RefreshCamera(); //вызов метода 
            IsInit = true;
            MainCamera = CamList.First();
            MainCamera.LiveViewUpdated += MainCamera_LiveViewUpdated;
            MainCamera.DownloadReady += MainCamera_DownloadReady;
            MainCamera.StateChanged += MainCamera_StateChanged;

            string line;
            i = 0;
            string[] lines = new string[4];

            StreamReader file = new StreamReader("Setting.txt");

            while ((line = file.ReadLine()) != null)
            {
                Flines[i] = line;
                i++;
            }

            file.Close();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                IsInit = false;
                MainCamera?.Dispose();
                APIHandler?.Dispose();
                Application.Exit();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (MainCamera == null || !MainCamera.SessionOpen) return; //если камера не указана или не открыта сессия камеры, то 

            if (!MainCamera.IsLiveViewOn) e.Graphics.Clear(BackColor); //если не включен предпросмотр, то очистить поверхность pictureBox 
            else
            {
                lock (LvLock)//блокировка кода 
                {
                    if (Evf_Bmp != null)
                    {
                        LVBratio = LVBw / (float)LVBh; //нахождение соотношение ширины к высоте у pictureBox
                        LVration = Evf_Bmp.Width / (float)Evf_Bmp.Height; //нахождение соотношение ширины к высоте у изображения Evf_Bmp
                        if (LVBratio < LVration) //если соотношение pictureBox меньше соотношения Evf_Bmp
                        {
                            w = LVBw; //ширина равна ширине pictureBox
                            h = (int)(LVBw / LVration); //высота
                        }
                        else
                        {
                            w = (int)(LVBh * LVration);
                            h = LVBh;
                        }
                        e.Graphics.DrawImage(Evf_Bmp, 0, 0, w, h); //задание изображения в pictureBox
                    }
                }
            }
        }

        private void pictureBox1_SizeChanged(object sender, EventArgs e)
        {
            try
            {
                LVBw = pictureBox1.Width;
                LVBh = pictureBox1.Height;
                pictureBox1.Invalidate(); //обновляет поверхность pictureBox
            }
            catch (Exception ex) { ReportError(ex.Message, false); }//обработка исключений: любая ошибка вызвать метод и передать сообщение ошибки
        }

        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                if (!MainCamera.SessionOpen) //если сессия главной камеры не открыта 
                {
                    MainCamera.OpenSession(); //то открыть сессию
                    button3.Text = "Режим Галереи"; //и установить надпись кнопки
                    button4.Enabled = true;
                    button1.Enabled = true;
                    if (!MainCamera.IsLiveViewOn)//если не включен предпросмотр с камеры
                    {
                        MainCamera.StartLiveView();//то включить предпросмотр
                    }
                    pictureBox1.SizeMode = PictureBoxSizeMode.Normal;
                }
                else
                {
                    if (MainCamera.IsLiveViewOn)
                    {
                        MainCamera.StopLiveView();
                    }
                    MainCamera.CloseSession(); //иначе закрыть сессию 
                    button3.Text = "Режим Фото"; //и установить надпись кнопки
                    button4.Enabled = false;
                    button1.Enabled = false;
                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                }
            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
            }
        }

        private void APIHandler_CameraAdded(CanonAPI sender)
        {
            try { Invoke((Action)delegate { RefreshCamera(); }); } //
            catch (Exception ex) { ReportError(ex.Message, false); } //обработка исключений: любая ошибка вызвать метод и передать сообщение ошибки
        }

        private void RefreshCamera()
        {
            CamList = APIHandler.GetCameraList(); //получение списка камер
        }

        private void ReportError(string message, bool lockdown)//вывод отчет об ошибках
        {
            int errc;
            lock (ErrLock) { errc = ++ErrCount; } //блокировка кода и подсчет количества ошибок

            //if (lockdown) EnableUI(false);

            if (errc < 4) MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);//меньше 4 ошибок, вывод описания ошибки
            else if (errc == 4) MessageBox.Show("Many errors happened!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);//больше 4 ошибок, вывод случилось множество ошибок

            lock (ErrLock) { ErrCount--; } //блокировка кода и уменьшение 
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (TimePhoto > 0)
            {
                label1.Text = Convert.ToString(TimePhoto);
                TimePhoto--;
            }
            else
            {
                timer1.Stop();
                MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                MainCamera.SetCapacity(4096, int.MaxValue);
                MainCamera.TakePhoto();

                if (CountPhoto > 0)
                {
                    CountPhoto--;
                    TimePhoto = Convert.ToInt32(Flines[2]);
                    timer1.Start();
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Form2 form2 = new Form2();
            this.Hide();
            form2.ShowDialog();
            this.Show();
            string line;
            int i = 0;

            StreamReader file = new StreamReader("Setting.txt");

            while ((line = file.ReadLine()) != null)
            {
                Flines[i] = line;
                i++;
            }

            file.Close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                //CountPhoto = Convert.ToInt32(Flines[1]);
                TimePhoto = Convert.ToInt32(Flines[0]);
                CountPhoto = 0;
                Thread myTh1 = new Thread(new ThreadStart(Countdown));
                myTh1.Start();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (MainCamera.SessionOpen)
                {
                    if (MainCamera.IsLiveViewOn)
                    {
                        MainCamera.StopLiveView();
                    }
                    MainCamera.CloseSession(); //иначе закрыть сессию 
                    button3.Text = "Режим Фото"; //и установить надпись кнопки
                    button4.Enabled = false;
                    pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                }
                if (listView1.SelectedItems.Count != 0 && ImageArray.Count != 0)
                {
                    pictureBox1.Image = Image.FromFile(ImageArray[listView1.SelectedItems[0].ImageIndex]);
                }
            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                IsInit = false;
                MainCamera?.Dispose();
                APIHandler?.Dispose();
                Application.Exit();
            }
            catch (Exception ex) { ReportError(ex.Message, false); }
        }

        private void MainCamera_StateChanged(Camera sender, StateEventID eventID, int parameter)
        {
            try { if (eventID == StateEventID.Shutdown && IsInit) { Invoke((Action)delegate { MainCamera.CloseSession(); ; }); } } //если ИДсобытия равен ИДвыключения и IsInit = true, то  
            catch (Exception ex) { ReportError(ex.Message, false); }//обработка исключений: любая ошибка вызвать метод и передать сообщение ошибки
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                CountPhoto = Convert.ToInt32(Flines[1]);
                TimePhoto = Convert.ToInt32(Flines[0]);
                Thread myTh1 = new Thread(new ThreadStart(Countdown));
                myTh1.Start();
            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
            }
        }

        private void MainCamera_LiveViewUpdated(Camera sender, Stream img)
        {
            try
            {
                lock (LvLock) //блокировка кода
                {

                    Evf_Bmp?.Dispose();
                    Evf_Bmp = new Bitmap(img);
                }
                pictureBox1.Invalidate(); //обновляет поверхность pictureBox
            }
            catch (Exception ex) { ReportError(ex.Message, false); }//обработка исключений: любая ошибка вызвать метод и передать сообщение ошибки
        }

        private void MainCamera_DownloadReady(Camera sender, DownloadInfo Info)
        {
            try
            {
                var filename = String.Format("SNAP_{0}.jpg", DateTime.Now.ToString("yyyy_MM_dd, HH.mm.ss"));
                Info.FileName = filename;
                string dir = null; //объявление переменной со значением NULL
                Invoke((Action)delegate { dir = Flines[3]; });
                sender.DownloadFile(Info, dir);
                Invoke((Action)delegate { ListView_Updated();});
                //Invoke((Action)delegate { /*MainProgressBar.Value = 0; */});
            }
            catch (Exception ex) { ReportError(ex.Message, false); }//обработка исключений: любая ошибка вызвать метод и передать сообщение ошибки
        }

        public void ListView_Updated()
        {
            try
            {
                ImageArray.Clear();
                listView1.Items.Clear();
                imageList1.Images.Clear();

                foreach (var item in Directory.GetFiles(Flines[3], "*.jpg"))
                {
                    ImageArray.Add(item);
                }
                ImageArray.Reverse();

                i = 0;
                foreach (var item in ImageArray)
                {
                    imageList1.Images.Add(Image.FromFile(item));
                    listView1.Items.Add("", i);
                    i++;
                }
                
                if (!SeriesOn)
                {
                    if (MainCamera.SessionOpen)
                    {
                        if (MainCamera.IsLiveViewOn)
                        {
                            MainCamera.StopLiveView();
                        }
                        MainCamera.CloseSession(); //иначе закрыть сессию 
                        button3.Text = "Режим Фото"; //и установить надпись кнопки
                        button4.Enabled = false;
                        button1.Enabled = false;
                        pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;
                        pictureBox1.Image = Image.FromFile(ImageArray[0]);
                    }
                }

            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
            }
        }

        private void Countdown()
        {
            try
            {
                if (CountPhoto == 0)
                {
                    Invoke((Action)delegate { label1.Visible = true; });
                    while (TimePhoto > 0)
                    {
                        Invoke((Action)delegate { label1.Text = Convert.ToString(TimePhoto); });
                        TimePhoto--;
                        Thread.Sleep(1000);
                    }

                    Invoke((Action)delegate {
                        label1.Visible = false;
                        MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                        MainCamera.SetCapacity(4096, int.MaxValue);
                        MainCamera.TakePhoto();
                        label1.Visible = false;
                    });
                }
                else
                {
                    Invoke((Action)delegate { label2.Visible = true; });
                    SeriesOn = true;
                    while (CountPhoto > 0)
                    {
                        Invoke((Action)delegate { label1.Visible = true; });
                        Invoke((Action)delegate { label2.Text = Convert.ToString(CountPhoto); });
                        while (TimePhoto > 0)
                        {
                            Invoke((Action)delegate { label1.Text = Convert.ToString(TimePhoto); });
                            TimePhoto--;
                            Thread.Sleep(1000);
                            if (TimePhoto == 0)
                            {
                                Invoke((Action)delegate { label1.Visible = false; });
                            }
                        }
                        if (CountPhoto == 1)
                        {
                            SeriesOn = false;
                        }
                        Invoke((Action)delegate {
                            MainCamera.SetSetting(PropertyID.SaveTo, (int)SaveTo.Host);
                            MainCamera.SetCapacity(4096, int.MaxValue);
                            MainCamera.TakePhoto();
                        });
                        TimePhoto = Convert.ToInt32(Flines[2]);
                        CountPhoto--;
                        Thread.Sleep(2000);
                    }
                    Invoke((Action)delegate { label2.Visible = false; });
                    
                }
            }
            catch (Exception ex)
            {
                ReportError(ex.Message, false);
            }
        }
    }
}

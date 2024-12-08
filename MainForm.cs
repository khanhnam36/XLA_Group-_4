using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.IO.Ports;
using tesseract;
using System.Threading;
using System.Data.SqlClient;
using Capture = Emgu.CV.Capture;

namespace Auto_parking
{
    public partial class MainForm : Form
    {
        private DataProvider dataProvider = new DataProvider();

        // Constructor khởi tạo MainForm
        public MainForm()
        {
            InitializeComponent(); // Khởi tạo các thành phần giao diện
            init(); // Gọi hàm init để khởi tạo các cài đặt ban đầu
        }

        // Hàm khởi tạo các thành phần ban đầu
        private void init()
        {
            initBienSo(); // Gọi hàm khởi tạo dữ liệu liên quan đến biển số
            loadDgBienSo(); // Tải dữ liệu biển số lên DataGridView
        }

        // Hàm khởi tạo dữ liệu liên quan đến biển số
        private void initBienSo()
        {
            loadDgBienSo(); // Tải dữ liệu biển số lên DataGridView
        }

        // Hàm tải dữ liệu biển số lên DataGridView
        private void loadDgBienSo()
        {
            try
            {
                // Chuỗi kết nối tới cơ sở dữ liệu SQL Server
                string connectionString = "Data Source=DESKTOP-E1A4U7J\\SQLSERVER2022;Initial Catalog=Thong_Tin_Bien_So_Xe;Integrated Security=True";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open(); // Mở kết nối tới cơ sở dữ liệu

                    // Câu truy vấn để lấy thông tin mã tỉnh, biển số và tên tỉnh
                    string query = @"
    SELECT 
        b.ma_tinh AS 'Mã Tỉnh', -- Lấy mã tỉnh
        CONCAT(b.ma_tinh,'-', b.bien_so) AS 'Biển Số', -- Ghép mã tỉnh và biển số
        t.ten_tinh AS 'Tên Tỉnh' -- Lấy tên tỉnh
    FROM 
        tbl_bien_so_xe b
    JOIN 
        tbl_tinh t ON b.ma_tinh = t.ma_tinh"; // Kết hợp bảng tbl_bien_so_xe với bảng tbl_tinh dựa trên mã tỉnh

                    using (SqlDataAdapter adapter = new SqlDataAdapter(query, conn))
                    {
                        DataTable dt = new DataTable();
                        adapter.Fill(dt); // Đổ dữ liệu từ câu truy vấn vào DataTable

                        // Cấu hình DataGridView không tự động tạo cột
                        dgBienso.AutoGenerateColumns = false;
                        dgBienso.Columns.Clear(); // Xóa các cột hiện tại (nếu có)

                        // Thêm cột "Mã Tỉnh" vào DataGridView
                        dgBienso.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            HeaderText = "Mã Tỉnh", // Tiêu đề cột
                            DataPropertyName = "Mã Tỉnh" // Tên cột trong DataTable
                        });

                        // Thêm cột "Biển Số" vào DataGridView
                        dgBienso.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            HeaderText = "Biển Số", // Tiêu đề cột
                            DataPropertyName = "Biển Số" // Tên cột trong DataTable
                        });

                        // Thêm cột "Tên Tỉnh" vào DataGridView
                        dgBienso.Columns.Add(new DataGridViewTextBoxColumn
                        {
                            HeaderText = "Tên Tỉnh", // Tiêu đề cột
                            DataPropertyName = "Tên Tỉnh" // Tên cột trong DataTable
                        });

                        // Gán dữ liệu từ DataTable vào DataGridView
                        dgBienso.DataSource = dt;
                    }
                }
            }
            catch (Exception ex) // Bắt lỗi khi xảy ra lỗi
            {
                MessageBox.Show("Error loading data: " + ex.Message); // Hiển thị thông báo lỗi
            }
        }

        #region Define
        // Danh sách để lưu trữ các hình ảnh biển số xe được trích xuất từ khung hình
        List<Image<Bgr, byte>> PlateImagesList = new List<Image<Bgr, byte>>();

        // Hình ảnh biển số xe được xử lý cuối cùng
        Image Plate_Draw;

        // Danh sách để lưu trữ các ký tự nhận diện từ biển số xe
        List<string> PlateTextList = new List<string>();

        // Danh sách các hình chữ nhật đại diện cho vùng chứa ký tự trên biển số xe
        List<Rectangle> listRect = new List<Rectangle>();

        // Mảng PictureBox để hiển thị các ký tự của biển số xe
        PictureBox[] box = new PictureBox[12];

        // Đối tượng TesseractProcessor để nhận diện văn bản từ hình ảnh (tất cả ký tự)
        public TesseractProcessor full_tesseract = null;

        // Đối tượng TesseractProcessor để nhận diện văn bản từ hình ảnh (chỉ chữ cái)
        public TesseractProcessor ch_tesseract = null;

        // Đối tượng TesseractProcessor để nhận diện văn bản từ hình ảnh (chỉ số)
        public TesseractProcessor num_tesseract = null;

        // Đường dẫn đến thư mục chứa dữ liệu Tesseract
        private string m_path = Application.StartupPath + @"\data\";

        // Danh sách các đường dẫn hình ảnh
        private List<string> lstimages = new List<string>();

        // Ngôn ngữ nhận diện của Tesseract
        private const string m_lang = "eng";

        // Đối tượng Capture để truy cập webcam
        Capture capture = null;
        #endregion

        // Đối tượng giao diện xử lý hình ảnh
        ImageForm IF;

        // Hàm được gọi khi MainForm được tải
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Tải dữ liệu biển số vào DataGridView
            loadDgBienSo();
            try
            {
                // Khởi tạo đối tượng capture để lấy hình ảnh từ webcam
                capture = new Emgu.CV.Capture();
            }
            catch { }

            // Kích hoạt timer
            timer1.Enabled = true;

            // Khởi tạo form xử lý hình ảnh
            IF = new ImageForm();

            // Khởi tạo và cấu hình Tesseract để nhận diện đầy đủ ký tự
            full_tesseract = new TesseractProcessor();
            bool succeed = full_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            full_tesseract.SetVariable("tessedit_char_whitelist", "ABCDEFHKLMNPRSTVXY1234567890").ToString();

            // Khởi tạo và cấu hình Tesseract để nhận diện chữ cái
            ch_tesseract = new TesseractProcessor();
            succeed = ch_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            ch_tesseract.SetVariable("tessedit_char_whitelist", "ABCDEFHKLMNPRSTUVXY").ToString();

            // Khởi tạo và cấu hình Tesseract để nhận diện số
            num_tesseract = new TesseractProcessor();
            succeed = num_tesseract.Init(m_path, m_lang, 3);
            if (!succeed)
            {
                MessageBox.Show("Tesseract initialization failed. The application will exit.");
                Application.Exit();
            }
            num_tesseract.SetVariable("tessedit_char_whitelist", "1234567890").ToString();

            // Cập nhật đường dẫn dữ liệu
            m_path = System.Environment.CurrentDirectory + "\\";

            // Lấy danh sách cổng serial có sẵn
            string[] ports = SerialPort.GetPortNames();

            // Khởi tạo các PictureBox để hiển thị ký tự biển số
            for (int i = 0; i < box.Length; i++)
            {
                box[i] = new PictureBox();
            }
        }

        // Hàm xử lý sự kiện nút debug, hiển thị hoặc ẩn cửa sổ giao diện
        private void debug_btn_Click(object sender, EventArgs e)
        {
            if (IF.Visible == false)
            {
                IF.Show();
            }
            else
            {
                IF.Hide();
            }
        }

        // Cờ kiểm soát trạng thái cập nhật hình ảnh webcam
        bool success = true;

        // Hàm xử lý sự kiện timer, cập nhật hình ảnh từ webcam
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (success == true)
            {
                success = false; // Đảm bảo chỉ có một luồng thực hiện cùng lúc
                new Thread(() =>
                {
                    try
                    {
                        // Đặt kích thước khung hình webcam
                        capture.SetCaptureProperty(CAP_PROP.CV_CAP_PROP_FRAME_WIDTH, 640);
                        capture.SetCaptureProperty(CAP_PROP.CV_CAP_PROP_FRAME_HEIGHT, 480);
                        Image<Bgr, byte> cap = capture.QueryFrame(); // Lấy khung hình hiện tại

                        if (cap != null)
                        {
                            MethodInvoker mi = delegate
                            {
                                try
                                {
                                    // Cập nhật hình ảnh webcam lên PictureBox
                                    Bitmap bmp = cap.ToBitmap();
                                    pictureBox_WC.Image = bmp;
                                    IF.pictureBox4.Image = bmp;
                                    pictureBox_WC.Update();
                                    IF.pictureBox4.Update();
                                }
                                catch (Exception ex)
                                { }
                            };
                            if (InvokeRequired)
                                Invoke(mi);
                        }
                    }
                    catch (Exception) { }
                    success = true; // Đặt lại trạng thái cờ
                }).Start();
            }
        }

        // Hàm xử lý hình ảnh đầu vào, tìm biển số xe
        public void ProcessImage(string urlImage)
        {
            PlateImagesList.Clear(); // Xóa danh sách hình ảnh biển số trước đó
            PlateTextList.Clear(); // Xóa danh sách văn bản biển số trước đó

            // Mở file hình ảnh
            FileStream fs = new FileStream(urlImage, FileMode.Open, FileAccess.Read);
            Image img = Image.FromStream(fs);
            Bitmap image = new Bitmap(img);
            IF.pictureBox2.Image = image; // Hiển thị hình ảnh gốc lên PictureBox
            fs.Close();

            // Tìm biển số trong hình ảnh
            FindLicensePlate4(image, out Plate_Draw);
        }

        public static Bitmap RotateImage(Image image, float angle)
        {
            // Kiểm tra xem hình ảnh có rỗng không, nếu có thì ném ngoại lệ
            if (image == null)
                throw new ArgumentNullException("image");

            // Xác định tâm xoay của hình ảnh
            PointF offset = new PointF((float)image.Width / 2, (float)image.Height / 2);

            // Tạo một hình ảnh rỗng có cùng kích thước với hình ảnh gốc
            Bitmap rotatedBmp = new Bitmap(image.Width, image.Height);
            rotatedBmp.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            // Tạo đối tượng Graphics để xử lý việc vẽ trên hình ảnh rỗng
            Graphics g = Graphics.FromImage(rotatedBmp);

            // Dịch chuyển tâm hình ảnh về gốc tọa độ
            g.TranslateTransform(offset.X, offset.Y);

            // Thực hiện xoay hình ảnh theo góc chỉ định
            g.RotateTransform(angle);

            // Dịch chuyển hình ảnh trở lại vị trí ban đầu
            g.TranslateTransform(-offset.X, -offset.Y);

            // Vẽ lại hình ảnh đã xoay lên đối tượng đồ họa
            g.DrawImage(image, new PointF(0, 0));

            // Trả về hình ảnh đã được xoay
            return rotatedBmp;
        }

        private string Ocr(Bitmap image_s, bool isFull, bool isNum = false)
        {
            string temp = ""; // Biến để lưu chuỗi nhận diện
            Image<Gray, byte> src = new Image<Gray, byte>(image_s); // Chuyển hình ảnh thành ảnh xám
            double ratio = 1;

            // Lặp lại cho đến khi tỷ lệ điểm ảnh trắng trên toàn hình đạt yêu cầu
            while (true)
            {
                ratio = (double)CvInvoke.cvCountNonZero(src) / (src.Width * src.Height);
                if (ratio > 0.5) break; // Dừng nếu tỷ lệ vượt quá 50%
                src = src.Dilate(2); // Làm dày các điểm trắng
            }

            // Chuyển ảnh xám thành Bitmap để xử lý tiếp
            Bitmap image = src.ToBitmap();

            // Chọn bộ xử lý Tesseract dựa trên loại nhận diện
            TesseractProcessor ocr;
            if (isFull)
                ocr = full_tesseract; // Nhận diện đầy đủ
            else if (isNum)
                ocr = num_tesseract; // Chỉ nhận diện số
            else
                ocr = ch_tesseract; // Chỉ nhận diện chữ cái

            int cou = 0;
            ocr.Clear();
            ocr.ClearAdaptiveClassifier();
            temp = ocr.Apply(image); // Nhận diện văn bản từ hình ảnh

            // Tiếp tục xử lý nếu chuỗi nhận diện dài hơn 3 ký tự
            while (temp.Length > 3)
            {
                Image<Gray, byte> temp2 = new Image<Gray, byte>(image);
                temp2 = temp2.Erode(2); // Làm mỏng các điểm trắng
                image = temp2.ToBitmap();
                ocr.Clear();
                ocr.ClearAdaptiveClassifier();
                temp = ocr.Apply(image); // Thực hiện nhận diện lại
                cou++;
                if (cou > 10) // Giới hạn số lần thử
                {
                    temp = "";
                    break;
                }
            }
            // Trả về chuỗi đã nhận diện
            return temp;
        }

        public void FindLicensePlate2(Bitmap image)
        {
            // Kiểm tra hình ảnh đầu vào có hợp lệ không
            if (image == null)
                return;

            Bitmap src;
            Image dst = image;
            Image<Bgr, byte> frame_b = null; // Khung hình tốt nhất chứa biển số
            Image<Bgr, byte> plate_b = null; // Hình ảnh biển số tốt nhất
            double sum_b = 0; // Tổng điểm trắng tốt nhất

            // Thử xoay hình ảnh trong khoảng -45 đến 45 độ
            for (float i = -45; i <= 45; i = i + 5)
            {
                src = RotateImage(dst, i); // Xoay hình ảnh
                PlateImagesList.Clear();
                Image<Bgr, byte> frame = new Image<Bgr, byte>(src);

                // Chuyển đổi ảnh thành ảnh xám và tìm biển số bằng Haar Cascade
                using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src))
                {
                    var faces = grayframe.DetectHaarCascade(
                        new HaarCascade(Application.StartupPath + "\\output-hv-33-x25.xml"),
                        1.1, 8,
                        HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                        new Size(0, 0)
                    )[0];

                    foreach (var face in faces)
                    {
                        Image<Bgr, byte> tmp = frame.Copy(); // Sao chép vùng phát hiện
                        tmp.ROI = face.rect; // Cắt vùng chứa biển số

                        frame.Draw(face.rect, new Bgr(Color.Blue), 2); // Vẽ khung biển số

                        // Lưu hình ảnh biển số đã phát hiện
                        PlateImagesList.Add(tmp.Resize(500, 500, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC, true));
                    }
                }

                if (PlateImagesList.Count != 0)
                {
                    // Chuyển đổi biển số phát hiện thành ảnh xám và tìm cạnh
                    Image<Gray, byte> gr = new Image<Gray, byte>(
                        PlateImagesList[0].Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).ToBitmap());
                    Gray cannyThreshold = new Gray(gr.GetAverage().Intensity);
                    Gray cannyThresholdLinking = new Gray(gr.GetAverage().Intensity);
                    Image<Gray, byte> cannyEdges = gr.Canny(cannyThreshold, cannyThresholdLinking);

                    double sum = 0;
                    for (int j = 0; j < cannyEdges.Height - 1; j++)
                    {
                        for (int k = 0; k < cannyEdges.Width - 1; k++)
                        {
                            // Tính tổng điểm trắng ở viền ngoài
                            if (j < 20 || j > 180 || k < 20 || k > 180)
                            {
                                sum += cannyEdges.Data[j, k, 0];
                            }
                        }
                    }

                    // Cập nhật biển số tốt nhất nếu cần
                    if (sum_b == 0 || sum > sum_b)
                    {
                        frame_b = frame.Clone();
                        plate_b = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone();
                        sum_b = sum;
                    }
                }
            }

            // Hiển thị biển số tốt nhất lên PictureBox
            if (plate_b != null)
            {
                PlateImagesList.Add(plate_b);
                pictureBox_WC.Image = frame_b.ToBitmap();
                pictureBox_WC.Update();
            }
        }

        public void FindLicensePlate(Bitmap image, out Image plateDraw)
        {
            plateDraw = null; // Khởi tạo biến lưu kết quả đầu ra
            Image<Bgr, byte> frame = new Image<Bgr, byte>(image); // Chuyển đổi hình ảnh đầu vào sang định dạng Bgr để xử lý
            bool isface = false; // Biến kiểm tra xem có phát hiện biển số hay không

            using (Image<Gray, byte> grayframe = new Image<Gray, byte>(image)) // Chuyển đổi ảnh sang ảnh xám
            {
                var faces = grayframe.DetectHaarCascade( // Phát hiện biển số bằng Haar Cascade
                    new HaarCascade(Application.StartupPath + "\\output-hv-33-x25.xml"), 1.1, 8,
                    HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(0, 0))[0];

                foreach (var face in faces)
                {
                    Image<Bgr, byte> tmp = frame.Copy(); // Sao chép vùng ảnh chứa biển số
                    tmp.ROI = face.rect; // Lấy vùng biển số

                    frame.Draw(face.rect, new Bgr(Color.Blue), 2); // Vẽ khung quanh biển số
                    PlateImagesList.Add(tmp); // Thêm biển số vào danh sách
                    isface = true;
                }

                if (isface)
                {
                    Image<Bgr, byte> showimg = frame.Clone(); // Lưu ảnh kết quả có khung biển số
                    plateDraw = (Image)showimg.ToBitmap(); // Chuyển đổi sang Bitmap để xuất kết quả
                    IF.pictureBox2.Image = showimg.ToBitmap(); // Hiển thị ảnh trên PictureBox
                    if (PlateImagesList.Count > 1) // Chọn biển số có kích thước lớn nhất
                    {
                        for (int i = 1; i < PlateImagesList.Count; i++)
                        {
                            if (PlateImagesList[0].Width < PlateImagesList[i].Width)
                            {
                                PlateImagesList[0] = PlateImagesList[i];
                            }
                        }
                    }
                    // Thay đổi kích thước biển số để chuẩn hóa
                    PlateImagesList[0] = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                }
            }
        }

        public void FindLicensePlate4(Bitmap image, out Image plateDraw)
        {
            plateDraw = null; // Biến kết quả đầu ra
            Bitmap src; // Hình ảnh sau khi xoay
            Image dst = image; // Gốc của hình ảnh đầu vào
            HaarCascade haar = new HaarCascade(Application.StartupPath + "\\output-hv-33-x25.xml"); // Đường dẫn mô hình Haar Cascade

            for (float i = 0; i <= 20; i = i + 3) // Xoay hình ảnh từ 0 đến 20 độ, mỗi lần tăng 3 độ
            {
                for (float s = -1; s <= 1 && s + i != 1; s += 2) // Thử cả hai hướng xoay (trái và phải)
                {
                    src = RotateImage(dst, i * s); // Xoay hình ảnh
                    PlateImagesList.Clear(); // Làm trống danh sách biển số
                    Image<Bgr, byte> frame = new Image<Bgr, byte>(src);

                    using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src)) // Chuyển ảnh sang ảnh xám
                    {
                        var faces = grayframe.DetectHaarCascade(haar, 1.1, 8, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(0, 0))[0];
                        foreach (var face in faces)
                        {
                            Image<Bgr, byte> tmp = frame.Copy(); // Sao chép vùng phát hiện
                            tmp.ROI = face.rect; // Lấy vùng biển số
                            frame.Draw(face.rect, new Bgr(Color.Blue), 2); // Vẽ khung biển số
                            PlateImagesList.Add(tmp);
                        }

                        if (PlateImagesList.Count > 0) // Nếu phát hiện biển số
                        {
                            plateDraw = frame.Clone().ToBitmap(); // Gán ảnh kết quả
                            PlateImagesList[0] = PlateImagesList[0].Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR);
                            return;
                        }
                    }
                }
            }
        }

        public void FindLicensePlate3(Bitmap image)
        {
            // Kiểm tra hình ảnh đầu vào có rỗng không
            if (image == null)
                return;

            Bitmap src; // Hình ảnh sau khi xoay
            Image dst = image; // Gán hình ảnh gốc đầu vào
            Image<Bgr, byte> frame_b = null; // Biến để lưu khung hình tốt nhất (chứa biển số)
            Image<Bgr, byte> plate_b = null; // Biến để lưu hình ảnh biển số tốt nhất
            double sum_b = 1000; // Biến để lưu giá trị tổng nhỏ nhất được tìm thấy
            HaarCascade haar = new HaarCascade(Application.StartupPath + "\\output-hv-33-x25.xml"); // Mô hình Haar Cascade

            // Lặp qua các góc xoay từ 0 đến 35 độ, mỗi bước tăng 3 độ
            for (float i = 0; i <= 35; i = i + 3)
            {
                // Thử xoay hình ảnh cả hai hướng (âm và dương)
                for (float s = -1; s <= 1 && s + i != 1; s += 2)
                {
                    src = RotateImage(dst, i * s); // Xoay hình ảnh
                    PlateImagesList.Clear(); // Xóa danh sách hình ảnh biển số trước đó
                    Image<Bgr, byte> frame = new Image<Bgr, byte>(src); // Chuyển đổi hình ảnh đã xoay sang định dạng Bgr

                    // Chuyển đổi sang ảnh xám và thực hiện phát hiện đối tượng
                    using (Image<Gray, byte> grayframe = new Image<Gray, byte>(src))
                    {
                        // Phát hiện biển số xe bằng Haar Cascade
                        var faces = grayframe.DetectHaarCascade(
                            haar, 1.1, 8, HAAR_DETECTION_TYPE.DO_CANNY_PRUNING, new Size(0, 0))[0];

                        // Lặp qua từng vùng phát hiện
                        foreach (var face in faces)
                        {
                            Image<Bgr, byte> tmp = frame.Copy(); // Sao chép khung hình chứa biển số
                            tmp.ROI = face.rect; // Lấy vùng phát hiện (biển số)

                            frame.Draw(face.rect, new Bgr(Color.Blue), 2); // Vẽ khung chữ nhật lên hình ảnh
                            PlateImagesList.Add(tmp.Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC)); // Lưu biển số vào danh sách
                        }
                    }

                    // Nếu tìm thấy ít nhất một biển số
                    if (PlateImagesList.Count != 0)
                    {
                        // Lấy biển số đầu tiên và chuyển sang ảnh xám
                        Image<Gray, byte> src2 = new Image<Gray, byte>(PlateImagesList[0].ToBitmap());
                        double thr = src2.GetAverage().Intensity; // Tính giá trị trung bình của ảnh xám

                        // Đặt ngưỡng tối thiểu và tối đa
                        double min = 0, max = 255;
                        if (thr - 50 > 0) min = thr - 50;
                        if (thr + 50 < 255) max = thr + 50;

                        // Lặp qua các giá trị ngưỡng để tìm vùng biển số tốt nhất
                        for (double value = min; value <= max; value += 5)
                        {
                            src2 = new Image<Gray, byte>(PlateImagesList[0].ToBitmap()); // Tải lại ảnh biển số
                            int c = 0; // Đếm số lượng vùng phù hợp
                            List<Rectangle> listR = new List<Rectangle>(); // Danh sách chứa các vùng phát hiện

                            using (MemStorage storage = new MemStorage())
                            {
                                // Áp dụng ngưỡng nhị phân cho ảnh
                                src2 = src2.ThresholdBinary(new Gray(value), new Gray(255));

                                // Tìm các đường viền trong ảnh đã nhị phân hóa
                                Contour<Point> contours = src2.FindContours(
                                    Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                                    Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST, storage);

                                // Lặp qua từng đường viền
                                while (contours != null)
                                {
                                    Rectangle rect = contours.BoundingRectangle; // Lấy vùng bao quanh đường viền
                                    double ratio = (double)rect.Width / rect.Height; // Tính tỷ lệ giữa chiều rộng và chiều cao

                                    // Kiểm tra nếu vùng phát hiện thỏa mãn kích thước và tỷ lệ
                                    if (rect.Width > 20 && rect.Width < 150 &&
                                        rect.Height > 80 && rect.Height < 180 &&
                                        ratio > 0.2 && ratio < 1.1)
                                    {
                                        c++; // Tăng số lượng vùng phù hợp
                                        listR.Add(contours.BoundingRectangle); // Thêm vùng vào danh sách
                                    }

                                    contours = contours.HNext; // Chuyển sang đường viền tiếp theo
                                }
                            }

                            double sum = 1000; // Giá trị tổng của sự khác biệt giữa các vùng
                            if (c >= 2) // Nếu tìm thấy ít nhất hai vùng phù hợp
                            {
                                // So sánh các vùng để tìm hai vùng có vị trí gần nhau nhất (theo trục Y)
                                for (int u = 0; u < c; u++)
                                {
                                    for (int v = u + 1; v < c; v++)
                                    {
                                        if (Math.Abs(listR[u].Y - listR[v].Y) < sum)
                                        {
                                            sum = Math.Abs(listR[u].Y - listR[v].Y);

                                            // Nếu hai vùng đủ gần, thêm vào danh sách biển số
                                            if (sum < 4)
                                            {
                                                PlateImagesList.Add(PlateImagesList[0]
                                                    .Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone());
                                                pictureBox_CarOut.Image = frame.ToBitmap(); // Hiển thị hình ảnh kết quả
                                                pictureBox_CarOut.Update();
                                                return; // Thoát hàm khi tìm thấy biển số tốt
                                            }
                                        }
                                    }
                                }
                            }

                            // Cập nhật vùng biển số tốt nhất nếu tổng mới nhỏ hơn tổng hiện tại
                            if (sum < sum_b)
                            {
                                frame_b = frame.Clone(); // Cập nhật khung hình tốt nhất
                                plate_b = PlateImagesList[0]
                                    .Resize(400, 400, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Clone();
                                sum_b = sum;
                            }
                        }
                    }
                }
            }

            // Hiển thị biển số tốt nhất nếu tìm thấy
            if (plate_b != null)
            {
                PlateImagesList.Add(plate_b); // Thêm biển số vào danh sách
                pictureBox_CarOut.Image = frame_b.ToBitmap(); // Hiển thị khung hình tốt nhất
                pictureBox_CarOut.Update();
            }
        }

        private void Reconize(string link, out Image hinhbienso, out string bienso, out string bienso_text)
        {
            // Loại bỏ các PictureBox cũ hiển thị ký tự biển số trên giao diện
            for (int i = 0; i < box.Length; i++)
            {
                this.Controls.Remove(box[i]);
            }

            // Khởi tạo giá trị đầu ra
            hinhbienso = null; // Hình ảnh chứa biển số đã phát hiện
            bienso = "";       // Chuỗi biển số đầy đủ
            bienso_text = "";  // Biển số với định dạng thô (chưa làm sạch)

            // Gọi hàm xử lý hình ảnh đầu vào
            ProcessImage(link);

            // Nếu đã phát hiện ít nhất một biển số
            if (PlateImagesList.Count != 0)
            {
                // Lấy biển số đầu tiên từ danh sách
                Image<Bgr, byte> src = new Image<Bgr, byte>(PlateImagesList[0].ToBitmap());
                Bitmap grayframe; // Ảnh xám sau khi phát hiện vùng ký tự
                FindContours con = new FindContours(); // Đối tượng để tìm đường viền
                Bitmap color; // Ảnh màu sau khi hiển thị các vùng phát hiện
                int c = con.IdentifyContours(src.ToBitmap(), 50, false, out grayframe, out color, out listRect);
                // Tìm các vùng chứa ký tự và lưu vào danh sách `listRect`

                // Hiển thị kết quả phát hiện trên giao diện
                pictureBox_PlateIn.Image = color; // Ảnh biển số có các khung vùng phát hiện
                IF.pictureBox1.Image = color;     // Hiển thị trên form phụ
                hinhbienso = Plate_Draw;          // Gán hình ảnh biển số đã vẽ
                pictureBox_PlateOut.Image = grayframe; // Ảnh xám sau xử lý
                IF.pictureBox3.Image = grayframe;     // Hiển thị trên form phụ

                // Tạo một ảnh xám từ hình ảnh vùng phát hiện
                Image<Gray, byte> dst = new Image<Gray, byte>(grayframe);
                grayframe = dst.ToBitmap(); // Chuyển về định dạng Bitmap để xử lý tiếp

                string zz = ""; // Chuỗi lưu biển số nhận diện

                // Các danh sách phục vụ sắp xếp và lọc vùng ký tự
                List<Rectangle> up = new List<Rectangle>();  // Các ký tự dòng trên
                List<Rectangle> dow = new List<Rectangle>(); // Các ký tự dòng dưới

                // Các biến phục vụ phân chia vùng
                int up_y = 0, dow_y = 0;
                bool flag_up = false;

                // Nếu không tìm thấy danh sách vùng, thoát hàm
                if (listRect == null) return;

                // **Xử lý và nhận diện ký tự từng vùng**
                for (int i = 0; i < listRect.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(listRect[i], grayframe.PixelFormat); // Cắt vùng ký tự
                    int cou = 0; // Biến đếm số lần xử lý OCR thất bại
                    full_tesseract.Clear(); // Xóa bộ nhớ OCR
                    full_tesseract.ClearAdaptiveClassifier(); // Xóa bộ nhớ từ vựng OCR
                    string temp = full_tesseract.Apply(ch); // Nhận diện ký tự trong vùng

                    // Nếu chuỗi nhận diện quá dài, tiếp tục xử lý
                    while (temp.Length > 3)
                    {
                        Image<Gray, byte> temp2 = new Image<Gray, byte>(ch);
                        temp2 = temp2.Erode(2); // Làm mỏng các nét ký tự
                        ch = temp2.ToBitmap();
                        full_tesseract.Clear();
                        full_tesseract.ClearAdaptiveClassifier();
                        temp = full_tesseract.Apply(ch);
                        cou++;
                        if (cou > 10) // Dừng xử lý nếu vượt quá số lần giới hạn
                        {
                            listRect.RemoveAt(i); // Loại bỏ vùng không phù hợp
                            i--;
                            break;
                        }
                    }
                }

                // **Phân chia các vùng thành dòng trên và dòng dưới**
                for (int i = 0; i < listRect.Count; i++)
                {
                    for (int j = i; j < listRect.Count; j++)
                    {
                        if (listRect[i].Y > listRect[j].Y + 100)
                        {
                            flag_up = true; // Đánh dấu vùng có ký tự dòng trên
                            up_y = listRect[j].Y;
                            dow_y = listRect[i].Y;
                            break;
                        }
                        else if (listRect[j].Y > listRect[i].Y + 100)
                        {
                            flag_up = true; // Đánh dấu vùng có ký tự dòng dưới
                            up_y = listRect[i].Y;
                            dow_y = listRect[j].Y;
                            break;
                        }
                        if (flag_up == true) break;
                    }
                }

                // Thêm các vùng vào danh sách dòng trên và dòng dưới
                for (int i = 0; i < listRect.Count; i++)
                {
                    if (listRect[i].Y < up_y + 50 && listRect[i].Y > up_y - 50)
                    {
                        up.Add(listRect[i]);
                    }
                    else if (listRect[i].Y < dow_y + 50 && listRect[i].Y > dow_y - 50)
                    {
                        dow.Add(listRect[i]);
                    }
                }
                if (flag_up == false) dow = listRect; // Nếu không phân dòng được, coi toàn bộ là dòng dưới

                // **Sắp xếp các vùng ký tự theo trục X**
                up.Sort((a, b) => a.X.CompareTo(b.X)); // Dòng trên
                dow.Sort((a, b) => a.X.CompareTo(b.X)); // Dòng dưới

                // **Nhận diện và hiển thị ký tự dòng trên**
                int x = 12; // Vị trí hiển thị ký tự trên giao diện
                int c_x = 0; // Đếm số ký tự đã hiển thị
                for (int i = 0; i < up.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(up[i], grayframe.PixelFormat); // Lấy vùng ký tự
                    string temp = Ocr(ch, false, i < 2); // Nhận diện ký tự (số hoặc chữ)
                    zz += temp; // Thêm ký tự vào chuỗi biển số

                    // Hiển thị ký tự trên giao diện
                    box[i].Location = new Point(x + i * 50, 290);
                    box[i].Size = new Size(50, 100);
                    box[i].SizeMode = PictureBoxSizeMode.StretchImage;
                    box[i].Image = ch;
                    box[i].Update();
                    IF.Controls.Add(box[i]);
                    c_x++;
                }

                // **Nhận diện và hiển thị ký tự dòng dưới**
                for (int i = 0; i < dow.Count; i++)
                {
                    Bitmap ch = grayframe.Clone(dow[i], grayframe.PixelFormat);
                    string temp = Ocr(ch, false, true); // Nhận diện ký tự
                    zz += temp; // Thêm ký tự vào chuỗi biển số
                    box[i + c_x].Location = new Point(x + i * 50, 390);
                    box[i + c_x].Size = new Size(50, 100);
                    box[i + c_x].SizeMode = PictureBoxSizeMode.StretchImage;
                    box[i + c_x].Image = ch;
                    box[i + c_x].Update();
                    IF.Controls.Add(box[i + c_x]);
                }

                // Làm sạch chuỗi biển số và gán kết quả
                bienso = zz.Replace("\n", "").Replace("\r", "");
                bienso_text = zz;
                IF.textBox6.Text = zz; // Hiển thị kết quả trên form phụ
            }
        }

        private void regonizeBtn_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Image (*.bmp; *.jpg; *.jpeg; *.png) |*.bmp; *.jpg; *.jpeg; *.png|All files (*.*)|*.*||";
            dlg.InitialDirectory = Application.StartupPath + "\\ImageTest";
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.Cancel)
            {
                return;
            }
            string startupPath = dlg.FileName;
            Image temp1;
            string temp2, temp3;
            Reconize(startupPath, out temp1, out temp2, out temp3);
            pictureBox_CarIn.Image = temp1;
            if (temp3 == "")
            {
                text_PlateIn.Text = "Không nhận dạng được biển số";
            }
            else
            {
                text_PlateIn.Text = temp3;

                // Bắt đầu tách mã tỉnh và biển số sau khi nhận dạng
                string plateData = text_PlateIn.Text;

                if (!string.IsNullOrEmpty(plateData))
                {
                    int separatorIndex = -1;

                    // Tìm vị trí ký tự in hoa đầu tiên (để phân tách mã tỉnh và biển số)
                    for (int i = 0; i < plateData.Length; i++)
                    {
                        if (char.IsUpper(plateData[i]))
                        {
                            separatorIndex = i;
                            break;
                        }
                    }
                    // Nếu tìm thấy ký tự in hoa
                    if (separatorIndex > 0)
                    {
                        // Mã tỉnh là phần trước ký tự in hoa
                        string provinceCode = plateData.Substring(0, separatorIndex);

                        // Biển số là phần từ ký tự in hoa trở đi (bao gồm ký tự in hoa)
                        string plateNumber = plateData.Substring(separatorIndex);

                        // Gán vào TextBox
                        txtMaTinh.Text = provinceCode;
                        txtBienSo.Text = plateNumber;
                    }
                    else
                    {
                        MessageBox.Show("Dữ liệu không hợp lệ, không tìm thấy ký tự phân cách!");
                    }
                }
                else
                {
                    MessageBox.Show("Không có dữ liệu trong text_PlateIn!");
                }
            }
        }

        private void capCameraBtn_Click(object sender, EventArgs e)
        {
            if (capture != null)
            {
                timer1.Enabled = false;
                pictureBox_CarOut.Image = null;
                IF.pictureBox2.Image = null;
                capture.QueryFrame().Save("aa.bmp");
                FileStream fs = new FileStream(m_path + "aa.bmp", FileMode.Open, FileAccess.Read);
                Image temp = Image.FromStream(fs);
                fs.Close();
                pictureBox_CarOut.Image = temp;
                IF.pictureBox2.Image = temp;
                pictureBox_CarOut.Update();
                IF.pictureBox2.Update();
                Image temp1;
                string temp2, temp3;
                Reconize(m_path + "aa.bmp", out temp1, out temp2, out temp3);
                pictureBox_CarIn.Image = temp1;
                if (temp3 == "")
                {
                    text_PlateIn.Text = "Không nhận dạng được biển số";
                }
                else
                {
                    text_PlateIn.Text = temp3;

                    // Bắt đầu tách mã tỉnh và biển số sau khi nhận dạng
                    string plateData = text_PlateIn.Text;

                    if (!string.IsNullOrEmpty(plateData))
                    {
                        int separatorIndex = -1;

                        // Tìm vị trí ký tự in hoa đầu tiên (để phân tách mã tỉnh và biển số)
                        for (int i = 0; i < plateData.Length; i++)
                        {
                            if (char.IsUpper(plateData[i]))
                            {
                                separatorIndex = i;
                                break;
                            }
                        }

                        // Nếu tìm thấy ký tự in hoa
                        if (separatorIndex > 0)
                        {
                            // Mã tỉnh là phần trước ký tự in hoa
                            string provinceCode = plateData.Substring(0, separatorIndex);

                            // Biển số là phần từ ký tự in hoa trở đi (bao gồm ký tự in hoa)
                            string plateNumber = plateData.Substring(separatorIndex);

                            // Gán vào TextBox
                            txtMaTinh.Text = provinceCode;
                            txtBienSo.Text = plateNumber;
                        }
                        else
                        {
                            MessageBox.Show("Dữ liệu không hợp lệ, không tìm thấy ký tự phân cách!");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Không có dữ liệu trong text_PlateIn!");
                    }
                }

                timer1.Enabled = true;
            }

        }

        #region WEBCAM
        // Khai báo mảng lưu trữ các đối tượng WEBCAM, cho phép quản lý tối đa 3 webcam cùng lúc.
        WEBCAM[] cam = new WEBCAM[3];
        /// Xử lý sự kiện khi người dùng nhấn chuột phải vào một PictureBox.
        ///Đối tượng phát sinh sự kiện (PictureBox)
        ///  tin về sự kiện chuột
        private void pictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            // Kiểm tra nếu người dùng nhấn chuột phải
            if (e.Button == MouseButtons.Right)
            {
                // Lấy PictureBox mà người dùng đã nhấn chuột
                PictureBox p = (PictureBox)sender;

                // Kiểm tra từng webcam đang chạy
                for (int i = 0; i < cam.Length; i++)
                {
                    if (cam[i] != null && cam[i].status == "run" && cam[i].pb == p.Name)
                    {
                        // Nếu webcam đang chạy và liên kết với PictureBox này, dừng webcam và giải phóng đối tượng
                        cam[i].Stop();
                        cam[i] = null;
                    }
                }

                // Tạo menu ngữ cảnh (context menu) để hiển thị danh sách webcam
                ContextMenu m = new ContextMenu();

                // Lấy danh sách tất cả các webcam có sẵn
                List<string> ls = WEBCAM.get_all_cam();

                // Thêm tối đa 3 webcam vào menu ngữ cảnh
                for (int i = 0; i <= 2 & i < ls.Count; i++)
                {
                    // Tạo một mục menu cho mỗi webcam, và gán hành động khi người dùng chọn webcam
                    m.MenuItems.Add(ls[i], (s, e2) =>
                    {
                        // Lấy thông tin menu item và menu ngữ cảnh cha
                        MenuItem menuItem = s as MenuItem;
                        ContextMenu owner = menuItem.Parent as ContextMenu;

                        // Lấy PictureBox mà menu ngữ cảnh thuộc về
                        PictureBox pb = (PictureBox)owner.SourceControl;

                        // Dừng webcam đang chạy nếu có
                        if (cam[menuItem.Index] != null && cam[menuItem.Index].status == "run")
                        {
                            cam[menuItem.Index].Stop();
                        }

                        // Khởi tạo một đối tượng WEBCAM mới
                        cam[menuItem.Index] = new WEBCAM();

                        // Bắt đầu webcam với chỉ mục tương ứng
                        cam[menuItem.Index].Start(menuItem.Index);

                        // Liên kết PictureBox với webcam
                        cam[menuItem.Index].put_picturebox(pb.Name);
                    });
                }

                // Hiển thị menu ngữ cảnh tại vị trí chuột
                m.Show(p, new Point(e.X, e.Y));
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            try
            {
                for (int i = 0; i < cam.Length; i++)
                {
                    if (cam[i] != null && cam[i].status == "run" && cam[i].image != null)
                    {
                        MethodInvoker mi = delegate
                        {
                            PictureBox pb = this.Controls.Find(cam[i].pb, true).FirstOrDefault() as PictureBox;
                            pb.Image = cam[i].image;
                            pb.Update();
                            pb.Invalidate();
                        };
                        if (InvokeRequired)
                        {
                            Invoke(mi);
                            return;
                        }

                        PictureBox pb2 = this.Controls.Find(cam[i].pb, true).FirstOrDefault() as PictureBox;
                        pb2.Image = cam[i].image;
                        pb2.Update();
                        pb2.Invalidate();
                    }
                }
            }
            catch (Exception) { }
        }


        #endregion

        private void dgBienso_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            // Kiểm tra chỉ số hàng và cột
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0) // Đảm bảo người dùng không nhấn vào tiêu đề
            {
                // Lấy giá trị từ ô được nhấn
                string cellValue = dgBienso.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString();

                // Hiển thị thông tin giá trị ô
                MessageBox.Show($"Bạn đã chọn ô: {cellValue}");
            }
        }

        private void btnLuu_Click(object sender, EventArgs e)
        {
            try
            {
                // Kiểm tra các trường thông tin có rỗng không
                if (string.IsNullOrEmpty(txtMaTinh.Text) || txtMaTinh.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Vui lòng nhập mã tỉnh!");
                    return;
                }

                if (string.IsNullOrEmpty(txtBienSo.Text) || txtBienSo.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Vui lòng nhập biển số!");
                    return;
                }

                // Lấy mã tỉnh từ TextBox và loại bỏ khoảng trắng thừa
                string maTinhText = txtMaTinh.Text.Trim();

                // In ra mã tỉnh để kiểm tra giá trị nhận được
                Console.WriteLine("Mã tỉnh nhập vào: " + maTinhText); // Kiểm tra mã tỉnh đã đúng chưa

                // Loại bỏ tất cả ký tự không phải số (nếu có)
                string maTinhSanitized = new string(maTinhText.Where(char.IsDigit).ToArray());

                // Kiểm tra nếu mã tỉnh là số hợp lệ
                int maTinh;
                if (maTinhSanitized.Length == 0 || !int.TryParse(maTinhSanitized, out maTinh))
                {
                    MessageBox.Show("Mã tỉnh phải là một số hợp lệ! Vui lòng kiểm tra lại.");
                    return;
                }

                // Kiểm tra biển số có hợp lệ không
                string bienSo = txtBienSo.Text.Trim();
                if (string.IsNullOrEmpty(bienSo))
                {
                    MessageBox.Show("Biển số không được để trống!");
                    return;
                }

                // Kiểm tra xem biển số đã tồn tại trong cơ sở dữ liệu chưa
                string connectionString = "Data Source=DESKTOP-E1A4U7J\\SQLSERVER2022;Initial Catalog=Thong_Tin_Bien_So_Xe;Integrated Security=True";

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Kiểm tra biển số đã tồn tại hay chưa
                    string checkBienSoQuery = "SELECT COUNT(*) FROM tbl_bien_so_xe WHERE bien_so = @BienSo";
                    using (SqlCommand checkCmd = new SqlCommand(checkBienSoQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@BienSo", bienSo);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count > 0)
                        {
                            // Nếu biển số đã tồn tại, thông báo lỗi và không lưu
                            MessageBox.Show("Biển số này đã tồn tại trong cơ sở dữ liệu! Vui lòng kiểm tra lại.");
                            return;
                        }
                    }
                }

                // Lấy tên tỉnh từ cơ sở dữ liệu
                string tenTinh = GetTenTinhByMaTinh(maTinh);
                if (string.IsNullOrEmpty(tenTinh))  // Kiểm tra nếu tên tỉnh trống
                {
                    MessageBox.Show("Không tìm thấy tên tỉnh cho mã tỉnh này!");
                    return;
                }

                // Hiển thị tên tỉnh trong textbox
                txtTenTinh.Text = tenTinh;

                // Lưu dữ liệu vào cơ sở dữ liệu
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Kiểm tra mã tỉnh có tồn tại trong bảng tbl_tinh không
                    string checkQuery = "SELECT COUNT(*) FROM tbl_tinh WHERE ma_tinh = @MaTinh";
                    using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                    {
                        checkCmd.Parameters.AddWithValue("@MaTinh", maTinh);
                        int count = (int)checkCmd.ExecuteScalar();
                        if (count == 0)
                        {
                            MessageBox.Show("Mã tỉnh không tồn tại trong cơ sở dữ liệu!");
                            return;
                        }
                    }

                    // Lưu dữ liệu vào bảng biển số xe
                    string insertQuery = "INSERT INTO tbl_bien_so_xe (ma_tinh, bien_so, ten_tinh) VALUES (@MaTinh, @BienSo, @TenTinh)";
                    using (SqlCommand cmd = new SqlCommand(insertQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@MaTinh", maTinh);
                        cmd.Parameters.AddWithValue("@BienSo", bienSo);
                        cmd.Parameters.AddWithValue("@TenTinh", tenTinh);
                        cmd.ExecuteNonQuery();
                    }
                }

                // Thêm dữ liệu trực tiếp vào DataGridView
                DataTable dt = (DataTable)dgBienso.DataSource;
                if (dt != null)
                {
                    DataRow newRow = dt.NewRow();
                    newRow["Mã Tỉnh"] = maTinh;
                    newRow["Biển Số"] = bienSo;
                    newRow["Tên Tỉnh"] = tenTinh;
                    dt.Rows.Add(newRow);
                    dgBienso.DataSource = dt;
                }

                MessageBox.Show("Lưu thông tin thành công!");

                // Xóa nội dung các TextBox
                txtMaTinh.Text = string.Empty;
                txtBienSo.Text = string.Empty;
                txtTenTinh.Text = string.Empty;
                loadDgBienSo();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi: " + ex.Message);
            }
        }

        // Hàm lấy tên tỉnh từ mã tỉnh
        private string GetTenTinhByMaTinh(int maTinh)
        {
            string tenTinh = string.Empty;
            string connectionString = "Data Source=DESKTOP-E1A4U7J\\SQLSERVER2022;Initial Catalog=Thong_Tin_Bien_So_Xe;Integrated Security=True";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                string query = "SELECT ten_tinh FROM tbl_tinh WHERE ma_tinh = @MaTinh";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@MaTinh", maTinh);

                    object result = cmd.ExecuteScalar();
                    if (result != null)
                    {
                        tenTinh = result.ToString();
                    }
                }
            }

            return tenTinh;
        }

        private void btnTim_Click(object sender, EventArgs e)
        {
            try
            {
                // Kiểm tra nếu từ khóa tìm kiếm rỗng
                if (txtTimKiem.Text == null || txtTimKiem.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Vui lòng nhập từ khóa tìm kiếm!");
                    return;
                }

                string tuKhoa = txtTimKiem.Text.Trim();

                // Chuỗi kết nối cơ sở dữ liệu
                string connectionString = "Data Source=DESKTOP-E1A4U7J\\SQLSERVER2022;Initial Catalog=Thong_Tin_Bien_So_Xe;Integrated Security=True";

                // Kết nối tới cơ sở dữ liệu
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Câu lệnh SQL để tìm kiếm theo tên tỉnh, mã tỉnh và biển số
                    string query = @"
                SELECT 
                    b.ma_tinh AS 'Mã Tỉnh',
                    CONCAT(b.ma_tinh, '-', b.bien_so) AS 'Biển Số',
                    t.ten_tinh AS 'Tên Tỉnh'
                FROM 
                    tbl_bien_so_xe b
                JOIN 
                    tbl_tinh t ON b.ma_tinh = t.ma_tinh
                WHERE 
                    t.ten_tinh LIKE @TuKhoa    -- Tìm theo tên tỉnh
                    OR b.bien_so LIKE @TuKhoa  -- Tìm theo biển số
                    OR t.ma_tinh LIKE @TuKhoa; -- Tìm theo mã tỉnh";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        // Thêm tham số từ khóa tìm kiếm
                        cmd.Parameters.AddWithValue("@TuKhoa", "%" + tuKhoa + "%");

                        // Sử dụng SqlDataAdapter để điền dữ liệu vào DataTable
                        using (SqlDataAdapter adapter = new SqlDataAdapter(cmd))
                        {
                            DataTable dt = new DataTable();
                            adapter.Fill(dt);

                            if (dt.Rows.Count > 0)
                            {
                                dgBienso.DataSource = dt; // Hiển thị kết quả tìm kiếm trong DataGridView
                                MessageBox.Show($"Tìm thấy {dt.Rows.Count} kết quả!");
                            }
                            else
                            {
                                MessageBox.Show("Không tìm thấy thông tin nào!");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi khi tìm kiếm: " + ex.Message);
            }
        }


        private void txtTimKiem_TextChanged(object sender, EventArgs e)
        {

        }

        private void btnXoa_Click(object sender, EventArgs e)
        {
            try
            {
                // Kiểm tra xem có dòng nào được chọn hay không
                if (dgBienso.SelectedRows.Count == 0)
                {
                    MessageBox.Show("Vui lòng chọn một dòng để xóa.");
                    return;
                }

                // Lấy giá trị từ cột "Biển Số" và tách ra thành ma_tinh và bien_so
                string bienSoHienThi = dgBienso.SelectedRows[0].Cells[1].Value.ToString(); // Cột "Biển Số"
                string[] parts = bienSoHienThi.Split('-'); // Tách chuỗi theo dấu "-"
                if (parts.Length != 2)
                {
                    MessageBox.Show("Dữ liệu không hợp lệ. Vui lòng kiểm tra lại!");
                    return;
                }

                string maTinh = parts[0].Trim();  // Phần trước dấu "-" là mã tỉnh
                string bienSo = parts[1].Trim();  // Phần sau dấu "-" là biển số

                // Xác nhận trước khi xóa
                DialogResult result = MessageBox.Show("Bạn có chắc chắn muốn xóa biển số này?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    // Chuỗi kết nối cơ sở dữ liệu
                    string connectionString = "Data Source=DESKTOP-E1A4U7J\\SQLSERVER2022;Initial Catalog=Thong_Tin_Bien_So_Xe;Integrated Security=True";

                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // Câu lệnh SQL để xóa bản ghi
                        string deleteQuery = "DELETE FROM tbl_bien_so_xe WHERE ma_tinh = @MaTinh AND bien_so = @BienSo";
                        using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                        {
                            // Thêm tham số
                            cmd.Parameters.AddWithValue("@MaTinh", maTinh);
                            cmd.Parameters.AddWithValue("@BienSo", bienSo);

                            // Thực thi câu lệnh xóa
                            int rowsAffected = cmd.ExecuteNonQuery();
                            if (rowsAffected > 0)
                            {
                                MessageBox.Show("Xóa thành công!");
                                // Tải lại dữ liệu sau khi xóa
                                loadDgBienSo();
                            }
                            else
                            {
                                MessageBox.Show("Không tìm thấy bản ghi để xóa.");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi khi xóa: " + ex.Message);
            }
        }
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            try
            {
                // Gọi lại hàm loadDgBienSo để tải lại dữ liệu vào DataGridView
                loadDgBienSo();
                MessageBox.Show("Làm mới dữ liệu thành công!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã xảy ra lỗi khi làm mới dữ liệu: " + ex.Message);
            }
        }
    }
}

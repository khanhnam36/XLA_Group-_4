using System;
using System.Collections.Generic;
using AForge.Video;
using AForge.Video.DirectShow;
using System.Drawing;
using System.Windows.Forms;
namespace Auto_parking
{
    class WEBCAM
    {
        private bool DeviceExist = false; // Biến kiểm tra thiết bị camera có tồn tại hay không.
        private FilterInfoCollection videoDevices; // Danh sách các thiết bị video (webcam).
        private VideoCaptureDevice videoSource = null; // Nguồn video hiện tại.
        public List<string> list_cam = new List<string>(); // Danh sách tên các thiết bị camera.
        public Bitmap bitmap; // Khung hình hiện tại dưới dạng ảnh bitmap.
        public Image image; // Khung hình hiện tại dưới dạng ảnh `Image`.
        public string status = "Non!"; // Trạng thái của webcam ("run", "stop", "Non!").
        public string pb = ""; // Tên của PictureBox sử dụng để hiển thị video.

        /// <summary>
        /// </summary>
        /// <returns>Danh sách tên các camera.</returns>
        public static List<string> get_all_cam()
        {
            List<string> ls = new List<string>();
            try
            {
                // Lấy danh sách các thiết bị video.
                FilterInfoCollection videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (videoDevices.Count == 0)
                    throw new ApplicationException(); // Nếu không tìm thấy thiết bị, ném ngoại lệ.

                // Duyệt qua từng thiết bị và thêm tên vào danh sách.
                foreach (FilterInfo device in videoDevices)
                {
                    ls.Add(device.Name);
                }
            }
            catch (ApplicationException)
            {
                // Nếu xảy ra lỗi, trả về danh sách rỗng.
                ls.Clear();
            }
            return ls;
        }

        /// <summary>
        /// Làm mới danh sách các thiết bị camera.
        /// </summary>
        public void refesh()
        {
            try
            {
                // Lấy danh sách các thiết bị video.
                videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                list_cam.Clear(); // Xóa danh sách cũ.
                if (videoDevices.Count == 0)
                    throw new ApplicationException();

                DeviceExist = true; // Thiết lập trạng thái thiết bị tồn tại.
                foreach (FilterInfo device in videoDevices)
                {
                    list_cam.Add(device.Name); // Thêm tên thiết bị vào danh sách.
                }
            }
            catch (ApplicationException)
            {
                // Nếu xảy ra lỗi, đặt trạng thái không có thiết bị.
                DeviceExist = false;
                list_cam.Clear();
                list_cam.Add("No capture device on your system"); // Thông báo không có thiết bị.
            }
        }

        /// <summary>
        /// Khởi động webcam theo chỉ số trong danh sách thiết bị.
        /// </summary>
        /// <param name="index">Chỉ số của thiết bị trong danh sách.</param>
        public void Start(int index)
        {
            refesh(); // Làm mới danh sách thiết bị.
            if (DeviceExist)
            {
                // Khởi tạo nguồn video với thiết bị được chọn.
                videoSource = new VideoCaptureDevice(videoDevices[index].MonikerString);
                videoSource.NewFrame += new NewFrameEventHandler(video_NewFrame); // Sự kiện xử lý khung hình mới.
                CloseVideoSource(); // Đảm bảo không có nguồn video nào đang chạy.
                videoSource.DesiredFrameSize = new Size(640, 480); // Thiết lập kích thước khung hình.
                videoSource.Start(); // Bắt đầu quay video.
                status = "run"; // Cập nhật trạng thái.
            }
        }

        /// <summary>
        /// Dừng webcam.
        /// </summary>
        public void Stop()
        {
            if (videoSource.IsRunning)
            {
                CloseVideoSource(); // Đóng nguồn video.
                status = "stop"; // Cập nhật trạng thái.
            }
        }

        /// <summary>
        /// Xử lý khung hình mới khi có sự kiện `NewFrame`.
        /// </summary>
        /// <param name="sender">Nguồn sự kiện.</param>
        /// <param name="eventArgs">Thông tin về khung hình mới.</param>
        private void video_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            bitmap = (Bitmap)eventArgs.Frame.Clone(); // Lưu khung hình mới dưới dạng bitmap.
            image = bitmap; // Lưu khung hình dưới dạng Image.
        }

        /// <summary>
        /// Đóng nguồn video nếu đang chạy.
        /// </summary>
        private void CloseVideoSource()
        {
            if (!(videoSource == null))
                if (videoSource.IsRunning)
                {
                    videoSource.SignalToStop(); // Tín hiệu dừng quay.
                    videoSource = null; // Xóa nguồn video.
                }
        }

        /// <summary>
        /// Lưu khung hình hiện tại dưới dạng tệp ảnh.
        /// </summary>
        public void Save()
        {
            string path = Application.StartupPath + @"\data\" + "aa.bmp"; // Đường dẫn lưu tệp.
            image.Save(path); // Lưu ảnh với định dạng BMP.
        }

        /// <summary>
        /// Gán tên của PictureBox để hiển thị video.
        /// </summary>
        /// <param name="name">Tên của PictureBox.</param>
        public void put_picturebox(string name)
        {
            pb = name; // Lưu tên PictureBox vào biến.
        }
    }
}

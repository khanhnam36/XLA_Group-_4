using Emgu.CV.Structure;
using Emgu.CV;
using System.Collections.Generic;
using System.Drawing;
using System;

namespace Auto_parking
{
    // Lớp này dùng để tìm kiếm và nhận diện các đường bao trong ảnh, với chức năng chính là xác định hình dạng (contour).
    class FindContours
    {
        public int count = 0; // Biến đếm số lượng đường bao tìm được.

        /// <summary>
        /// Phương thức xử lý ảnh để tìm các đường bao và trả về ảnh kết quả.
        /// </summary>
        /// <param name="colorImage">Ảnh màu đầu vào.</param>
        /// <param name="thresholdValue">Ngưỡng để thực hiện phân ngưỡng (thresholding).</param>
        /// <param name="invert">Xác định có đảo ngược giá trị ngưỡng hay không.</param>
        /// <param name="processedGray">Ảnh kết quả ở định dạng grayscale.</param>
        /// <param name="processedColor">Ảnh màu kết quả sau xử lý.</param>
        /// <param name="list">Danh sách các hình chữ nhật bao quanh các đường bao được tìm thấy.</param>
        /// <returns>Số lượng đường bao tìm được.</returns>
        public int IdentifyContours(Bitmap colorImage, int thresholdValue, bool invert, out Bitmap processedGray, out Bitmap processedColor, out List<Rectangle> list)
        {
            List<Rectangle> listR = new List<Rectangle>(); // Danh sách các hình chữ nhật bao quanh đường bao.

            #region Chuyển đổi sang ảnh xám
            // Chuyển ảnh màu sang ảnh grayscale.
            Image<Gray, byte> grayImage = new Image<Gray, byte>(colorImage);
            // Tạo ảnh grayscale trống có kích thước giống ảnh gốc.
            Image<Gray, byte> bi = new Image<Gray, byte>(grayImage.Width, grayImage.Height);
            // Sao chép ảnh màu gốc.
            Image<Bgr, byte> color = new Image<Bgr, byte>(colorImage);
            #endregion

            #region Tìm ngưỡng để có số ký tự lớn nhất
            double thr = 0; // Giá trị ngưỡng ban đầu.
            if (thr == 0)
            {
                // Tính giá trị trung bình của ảnh xám làm giá trị ngưỡng.
                thr = grayImage.GetAverage().Intensity;
            }

            // Mảng chứa các hình chữ nhật tốt nhất (tối đa 9 phần tử).
            Rectangle[] li = new Rectangle[9];
            Image<Bgr, byte> color_b = new Image<Bgr, byte>(colorImage);
            Image<Gray, byte> src_b = grayImage.Clone(); // Bản sao của ảnh gốc.
            Image<Gray, byte> bi_b = bi.Clone();         // Bản sao của ảnh nhị phân.
            Image<Bgr, byte> color2;                     // Ảnh màu tạm để vẽ các đường bao.
            Image<Gray, byte> src;                       // Ảnh nguồn sau phân ngưỡng.
            Image<Gray, byte> bi2;                       // Ảnh nhị phân tạm.
            int c = 0, c_best = 0;                       // Số lượng đường bao và số lượng tốt nhất tìm được.

            for (double value = 0; value <= 127; value += 3)
            {
                for (int s = -1; s <= 1 && s + value != 1; s += 2)
                {
                    // Tạo bản sao ảnh màu và ảnh nhị phân.
                    color2 = new Image<Bgr, byte>(colorImage);
                    bi2 = bi.Clone();
                    listR.Clear();
                    c = 0;
                    double t = 127 + value * s; // Tính giá trị ngưỡng.

                    // Phân ngưỡng ảnh xám để tạo ảnh nhị phân.
                    src = grayImage.ThresholdBinary(new Gray(t), new Gray(255));

                    using (MemStorage storage = new MemStorage())
                    {
                        // Tìm các đường bao trong ảnh nhị phân.
                        Contour<Point> contours = src.FindContours(
                            Emgu.CV.CvEnum.CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE,
                            Emgu.CV.CvEnum.RETR_TYPE.CV_RETR_LIST,
                            storage);

                        while (contours != null)
                        {
                            // Lấy hình chữ nhật bao quanh mỗi đường bao.
                            Rectangle rect = contours.BoundingRectangle;

                            // Vẽ đường bao ban đầu bằng màu xanh dương.
                            CvInvoke.cvDrawContours(color2, contours,
                                new MCvScalar(255, 255, 0),
                                new MCvScalar(0), -1, 1,
                                Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                                new Point(0, 0));

                            // Kiểm tra điều kiện hợp lệ cho đường bao.
                            double ratio = (double)rect.Width / rect.Height;
                            if (rect.Width > 20 && rect.Width < 150
                                && rect.Height > 80 && rect.Height < 180
                                && ratio > 0.1 && ratio < 1.1 && rect.X > 20)
                            {
                                c++;
                                // Vẽ đường bao hợp lệ bằng màu vàng.
                                CvInvoke.cvDrawContours(color2, contours,
                                    new MCvScalar(0, 255, 255),
                                    new MCvScalar(255), -1, 3,
                                    Emgu.CV.CvEnum.LINE_TYPE.EIGHT_CONNECTED,
                                    new Point(0, 0));

                                // Vẽ hình chữ nhật bao quanh đường bao.
                                color2.Draw(contours.BoundingRectangle, new Bgr(0, 255, 0), 2);

                                // Vẽ đường bao trên ảnh nhị phân.
                                bi2.Draw(contours, new Gray(255), -1);
                                listR.Add(contours.BoundingRectangle); // Thêm vào danh sách.
                            }
                            contours = contours.HNext; // Chuyển sang đường bao tiếp theo.
                        }
                    }

                    // Tính khoảng cách trung bình và loại bỏ đường bao bị chồng lấp.
                    double avg_h = 0;
                    double dis = 0;
                    for (int i = 0; i < c; i++)
                    {
                        avg_h += listR[i].Height;
                        for (int j = i + 1; j < c; j++)
                        {
                            if ((listR[j].X < (listR[i].X + listR[i].Width) && listR[j].X > listR[i].X)
                                && (listR[j].Y < (listR[i].Y + listR[i].Height) && listR[j].Y > listR[i].Y))
                            {
                                listR.RemoveAt(j);
                                c--;
                                j--;
                            }
                            else if ((listR[i].X < (listR[j].X + listR[j].Width) && listR[i].X > listR[j].X)
                                && (listR[i].Y < (listR[j].Y + listR[j].Height) && listR[i].Y > listR[j].Y))
                            {
                                avg_h -= listR[i].Height;
                                listR.RemoveAt(i);
                                c--;
                                i--;
                                break;
                            }
                        }
                    }
                    avg_h = avg_h / c; // Chiều cao trung bình.
                    for (int i = 0; i < c; i++)
                    {
                        dis += Math.Abs(avg_h - listR[i].Height);
                    }

                    // Cập nhật nếu số lượng đường bao tốt hơn.
                    if (c <= 8 && c > 1 && c > c_best && dis <= c * 8)
                    {
                        listR.CopyTo(li);
                        c_best = c;
                        color_b = color2;
                        bi_b = bi2;
                        src_b = src;
                    }
                }
                if (c_best == 8) break;
            }

            count = c_best; // Số lượng đường bao tốt nhất.
            grayImage = src_b;
            color = color_b;
            bi = bi_b;

            listR.Clear();
            for (int i = 0; i < li.Length; i++)
            {
                if (li[i].Height != 0) listR.Add(li[i]);
            }
            #endregion

            #region Gán kết quả đầu ra
            processedColor = color.ToBitmap(); // Ảnh màu kết quả.
            processedGray = grayImage.ToBitmap(); // Ảnh xám kết quả.
            list = listR; // Danh sách hình chữ nhật.
            #endregion

            return count; // Trả về số lượng đường bao tìm được.
        }
    }
}

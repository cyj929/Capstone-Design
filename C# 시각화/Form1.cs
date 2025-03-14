using System;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    // 선택한 이미지 경로
                    string imagePath = ofd.FileName;
                    pictureBox.Image = System.Drawing.Image.FromFile(imagePath);

                    // Flask API 호출
                    string apiUrl = "https://edeb-34-83-211-224.ngrok-free.app/predict_all"; // Flask API URL
                    string result = await SendImageToFlaskApi(apiUrl, imagePath);

                    // 파일 이름 추가
                    string fileName = Path.GetFileName(imagePath);

                    string displayResult = $"{result}";

                    // 결과 표시
                    txtResult.Text = displayResult;
                    txtFileName.Text = fileName;
                }
            }

        }

        private async Task<string> SendImageToFlaskApi(string apiUrl, string imagePath)
        {
            try
            {
                using (var client = new HttpClient())
                {
                    var content = new MultipartFormDataContent();
                    var imageContent = new ByteArrayContent(File.ReadAllBytes(imagePath));
                    imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
                    content.Add(imageContent, "file", Path.GetFileName(imagePath));

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseString = await response.Content.ReadAsStringAsync();
                        JObject jsonResponse = JObject.Parse(responseString);

                        // ViT 결과 처리 (최고 확률 클래스만 표시)
                        var vitResult = jsonResponse["vit_prediction"]?.ToObject<Dictionary<string, float>>();
                        string vitOutput = "어선 상태: ";
                        if (vitResult != null && vitResult.Count > 0)
                        {
                            var topClass = vitResult.OrderByDescending(kv => kv.Value).First();
                            vitOutput += $"{topClass.Key} ({topClass.Value * 100:F2}%)\n";
                        }

                        // YOLOv11 결과 처리 (최고 신뢰도 객체만 표시)
                        var yoloResult = jsonResponse["object_detection"]?.ToObject<List<JObject>>();
                        string yoloOutput = "어선 종류: ";
                        if (yoloResult != null && yoloResult.Count > 0)
                        {
                            var topObject = yoloResult.OrderByDescending(obj => (float)obj["confidence"]).First();
                            int objClass = (int)topObject["class"]; // YOLO 결과의 class 번호
                            string confidence = topObject["confidence"]?.ToString();

                            // 클래스 번호를 이름으로 변환
                            yoloOutput += $"{ClassNameMapping[objClass]} ({float.Parse(confidence) * 100:F2}%)\n";
                        }

                        // OCR 결과 처리
                        // OCR 결과 처리
                        var ocrResult = jsonResponse["ocr_and_classification"];
                        string ocrOutput = "좌표값 추출: ";
                        if (ocrResult != null)
                        {
                            if (ocrResult["Coordinates not found"] != null)
                            {
                                ocrOutput += "Coordinates not found\n";
                            }
                            else
                            {
                                string latitude = ocrResult["latitude"]?.ToString() ?? "N/A";
                                string longitude = ocrResult["longitude"]?.ToString() ?? "N/A";
                                string classification = ocrResult["classification"]?.ToString() ?? "N/A";
                                ocrOutput += $"{Environment.NewLine}Latitude: {latitude}," +
                                             $"{Environment.NewLine}Longitude: {longitude}," +
                                             $"{Environment.NewLine}Classification: {classification}\n";
                            }
                        }


                        // 결과 통합
                        return $"{vitOutput}{Environment.NewLine}{yoloOutput}{Environment.NewLine}{ocrOutput}";
                    }
                    else
                    {
                        return $"Error: {Environment.NewLine}{response.StatusCode}";
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error occurred: {ex.Message}");
                return $"Exception: {ex.Message}";
            }
        }





        private static readonly Dictionary<int, string> ClassNameMapping = new Dictionary<int, string>
        {
            { 0, "1. 낚시어선" },
            { 1, "10. 등광조망" },
            { 2, "2. 저인망" },
            { 3, "3. 채낚기" },
            { 4, "4. 연승" },
            { 5, "5. 통발" },
            { 6, "6. 안강망" },
            { 7, "7. 타망" },
            { 8, "8. 유망" },
            { 9, "9. 범장망" }
        };


    }
}

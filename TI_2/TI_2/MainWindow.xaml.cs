using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace TI_2
{
    public partial class MainWindow : Window
    {
        private byte[]? _inputBytes;
        private byte[]? _outputBytes;
        private string? _selectedFilePath;

        public MainWindow()
        {
            InitializeComponent();
        }

        // Полином: x^30 + x^16 + x^15 + x + 1
        private class Lfsr
        {
            private readonly int[] _state;
            private const int StateSize = 30;

            public Lfsr(string seedStr)
            {
                _state = new int[StateSize];
                for (int i = 0; i < StateSize && i < seedStr.Length; i++)
                    _state[i] = seedStr[i] == '1' ? 1 : 0;
            }

            public int NextBit()
            {
                int outputBit = _state[0];
                int feedback = _state[0] ^ _state[14] ^ _state[15] ^ _state[29];

                for (int i = 0; i < StateSize - 1; i++)
                    _state[i] = _state[i + 1];

                _state[StateSize - 1] = feedback;
                return outputBit;
            }

            public byte NextByte()
            {
                byte result = 0;
                for (int i = 0; i < 8; i++)
                    result = (byte)((result << 1) | NextBit());
                return result;
            }
        }

        private void SeedTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            string text = textBox.Text;
            string filtered = new string(text.Where(c => c == '0' || c == '1').ToArray());

            if (text != filtered)
            {
                int cursor = textBox.SelectionStart;
                textBox.Text = filtered;
                textBox.SelectionStart = Math.Min(cursor, filtered.Length);
            }

            CounterTextBlock.Text = $"Символов: {filtered.Length}/30";
            CounterTextBlock.Foreground = filtered.Length == 30
                ? System.Windows.Media.Brushes.Green
                : System.Windows.Media.Brushes.Gray;
        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { Title = "Выберите файл для обработки" };

            if (dialog.ShowDialog() != true) return;

            _selectedFilePath = dialog.FileName;
            FilePathTextBox.Text = _selectedFilePath;

            try
            {
                _inputBytes = File.ReadAllBytes(_selectedFilePath);
                MessageBox.Show($"Файл загружен. Размер: {_inputBytes.Length} байт",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при чтении файла: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            string seedStr = SeedTextBox.Text;

            if (seedStr.Length != 30)
            {
                MessageBox.Show("Введите ровно 30 бит (0 и 1)!", "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_inputBytes == null || _inputBytes.Length == 0)
            {
                MessageBox.Show("Выберите файл!", "Ошибка ввода",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ProcessButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = Visibility.Collapsed;

            try
            {
                var result = await Task.Run(() => ProcessFile(seedStr, _inputBytes));

                _outputBytes = result.OutputBytes;

                OrigOutput.Text = result.OrigDisplay;
                KeyOutput.Text = result.KeyDisplay;
                ResultOutput.Text = result.ResultDisplay;
                ResultsPanel.Visibility = Visibility.Visible;

                SaveButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ProcessButton.IsEnabled = true;
                ProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_outputBytes == null || _selectedFilePath == null) return;

            string ext = Path.GetExtension(_selectedFilePath);

            var dialog = new SaveFileDialog
            {
                FileName = "",
                Filter = "Все файлы (*.*)|*.*",
                DefaultExt = ext,
                InitialDirectory = Path.GetDirectoryName(_selectedFilePath) ?? ""
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllBytes(dialog.FileName, _outputBytes);
                MessageBox.Show($"Файл сохранён:\n{dialog.FileName}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static ProcessResult ProcessFile(string seedStr, byte[] inputBytes)
        {
            var lfsr = new Lfsr(seedStr);
            var outputBytes = new byte[inputBytes.Length];

            var origList = new List<string>(inputBytes.Length);
            var keyList  = new List<string>(inputBytes.Length);
            var resList  = new List<string>(inputBytes.Length);

            for (int i = 0; i < inputBytes.Length; i++)
            {
                byte keyByte = lfsr.NextByte();
                byte resByte = (byte)(inputBytes[i] ^ keyByte);
                outputBytes[i] = resByte;

                origList.Add(Convert.ToString(inputBytes[i], 2).PadLeft(8, '0'));
                keyList.Add(Convert.ToString(keyByte,        2).PadLeft(8, '0'));
                resList.Add(Convert.ToString(resByte,        2).PadLeft(8, '0'));
            }

            const int limit = 10;

            string Format(List<string> list)
            {
                if (list.Count <= limit * 2)
                    return string.Join(" ", list);
                return string.Join(" ", list.Take(limit))
                       + " ... "
                       + string.Join(" ", list.Skip(list.Count - limit));
            }

            return new ProcessResult
            {
                OutputBytes   = outputBytes,
                OrigDisplay   = Format(origList),
                KeyDisplay    = Format(keyList),
                ResultDisplay = Format(resList)
            };
        }
    }

    public class ProcessResult
    {
        public byte[] OutputBytes   { get; set; } = Array.Empty<byte>();
        public string OrigDisplay   { get; set; } = "";
        public string KeyDisplay    { get; set; } = "";
        public string ResultDisplay { get; set; } = "";
    }
}
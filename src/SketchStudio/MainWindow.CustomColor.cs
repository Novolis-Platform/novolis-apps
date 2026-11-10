using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace SketchStudio;

internal sealed partial class MainWindow
{
    sealed class CustomColorChannels(byte a, byte r, byte g, byte b)
    {
        public byte A = a;
        public byte R = r;
        public byte G = g;
        public byte B = b;
    }

    sealed class CustomColorDialogUi(
        CustomColorChannels channels,
        Border preview,
        TextBox hexBox,
        Slider rSlider,
        Slider gSlider,
        Slider bSlider,
        Slider aSlider,
        TextBlock rLabel,
        TextBlock gLabel,
        TextBlock bLabel,
        TextBlock aLabel)
    {
        readonly CustomColorChannels _channels = channels;
        readonly Border _preview = preview;
        readonly TextBox _hexBox = hexBox;
        readonly Slider _rSlider = rSlider;
        readonly Slider _gSlider = gSlider;
        readonly Slider _bSlider = bSlider;
        readonly Slider _aSlider = aSlider;
        readonly TextBlock _rLabel = rLabel;
        readonly TextBlock _gLabel = gLabel;
        readonly TextBlock _bLabel = bLabel;
        readonly TextBlock _aLabel = aLabel;

        public CustomColorChannels Channels => _channels;

        public void ApplySliderBatch()
        {
            ReadChannelsFromSliders();
            StampChannelLabels();
            StampPreviewAndHex();
        }

        public void SyncFromHex()
        {
            if (!TryParseColorComponents(_hexBox.Text?.Trim() ?? "", out var pa, out var pr, out var pg, out var pb))
                return;
            _channels.A = pa;
            _channels.R = pr;
            _channels.G = pg;
            _channels.B = pb;
            _hexBox.Tag = true;
            ApplySliderValuesWithoutStorm();
            _hexBox.Tag = false;
            ApplySliderBatch();
        }

        void ReadChannelsFromSliders()
        {
            _channels.R = (byte)Math.Clamp((int)Math.Round(_rSlider.Value), 0, 255);
            _channels.G = (byte)Math.Clamp((int)Math.Round(_gSlider.Value), 0, 255);
            _channels.B = (byte)Math.Clamp((int)Math.Round(_bSlider.Value), 0, 255);
            _channels.A = (byte)Math.Clamp((int)Math.Round(_aSlider.Value), 0, 255);
        }

        void ApplySliderValuesWithoutStorm()
        {
            _rSlider.Value = _channels.R;
            _gSlider.Value = _channels.G;
            _bSlider.Value = _channels.B;
            _aSlider.Value = _channels.A;
        }

        void StampChannelLabels()
        {
            _rLabel.Text = _channels.R.ToString();
            _gLabel.Text = _channels.G.ToString();
            _bLabel.Text = _channels.B.ToString();
            _aLabel.Text = _channels.A.ToString();
        }

        void StampPreviewAndHex()
        {
            _preview.Background = new SolidColorBrush(Color.FromArgb(_channels.A, _channels.R, _channels.G, _channels.B));
            if (_hexBox.Tag is not true)
                _hexBox.Text = FormatHex(_channels.A, _channels.R, _channels.G, _channels.B);
        }
    }
}

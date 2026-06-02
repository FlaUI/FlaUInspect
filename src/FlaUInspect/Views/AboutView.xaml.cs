using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace FlaUInspect.Views;

public partial class AboutView : UserControl, IDisposable {
	private readonly Random _rnd = new();
	private CancellationTokenSource? _colorLoopCts;
	private bool _disposedValue;

	public AboutView() {
		InitializeComponent();
		Loaded += (_, __) => {
			Regenerate();
			StartTextColorLoop();
		};
		SizeChanged += (_, __) => Regenerate();
	}

	private void StartTextColorLoop() {
		_colorLoopCts?.Cancel();
		_colorLoopCts = new CancellationTokenSource();

		_ = RunColorLoop(_colorLoopCts.Token);
	}

	private void Regenerate() {
		if (HudCanvas.ActualWidth <= 0 || HudCanvas.ActualHeight <= 0)
			return;

		HudCanvas.Children.Clear();

		var count = 50;
		var w = HudCanvas.ActualWidth;
		var h = HudCanvas.ActualHeight;

		for (var i = 0; i < count; i++) {
			Line line = new() {
				X1 = _rnd.NextDouble() * w,
				Y1 = _rnd.NextDouble() * h,
				X2 = _rnd.NextDouble() * w,
				Y2 = _rnd.NextDouble() * h,
				Style = (Style)Resources["HudLine"],
				Stroke = new SolidColorBrush(RandomHudColor())
			};

			_ = HudCanvas.Children.Add(line);

			var pulse = ((Storyboard)Resources["PulseLine"]).Clone();
			Storyboard.SetTarget(pulse, line);

			foreach (var t in pulse.Children)
				t.BeginTime = TimeSpan.FromMilliseconds(_rnd.Next(0, 1200));

			pulse.Begin();
		}
	}

	private async Task RunColorLoop(CancellationToken token) {
		while (!token.IsCancellationRequested) {
			var next = RandomHudColor();

			ColorAnimation anim = new() {
				To = next,
				Duration = TimeSpan.FromSeconds(2.5),
				EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
			};

			FlaUiControlBrush.BeginAnimation(SolidColorBrush.ColorProperty, anim);

			// пауза між переходами
			await Task.Delay(TimeSpan.FromSeconds(3.0), token);
		}
	}

	private Color RandomHudColor() {
		Color[] palette = [
			Color.FromArgb(180, 80, 200, 255),
			Color.FromArgb(180, 120, 160, 255),
			Color.FromArgb(180, 160, 120, 255),
			Color.FromArgb(180, 80, 255, 200),
			Color.FromArgb(180, 200, 120, 255)
		];

		return palette[_rnd.Next(palette.Length)];
	}

	protected virtual void Dispose(bool disposing) {
		if (!_disposedValue) {
			if (disposing) {
				_colorLoopCts?.Dispose();
				_colorLoopCts = null;
			}
			_disposedValue = true;
		}
	}

	public void Dispose() {
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}
}
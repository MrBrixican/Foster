using Foster.Framework;
using Foster.Framework.Audio;
using System.Numerics;

class Program
{
	public static void Main()
	{
		App.Register<Game>();
		App.Run("Hello Audio", 1280, 720);
	}
}

class Game : Module
{
	private static readonly Vector2 PlayBarSize = new(1000, 30);

	private readonly Batcher batch = new();
	private readonly Sound sound = new(Path.Join("Assets", "boss battle - star run.mp3"), SoundLoadingMethod.LoadOnDemand);
	private readonly SpriteFont font = new SpriteFont(Path.Join("Assets", "monogram.ttf"), 32);
	private SoundInstance instance;
	private double instanceLength;
	private Vector3 lastPosition;

	public override void Startup()
	{
		instance = sound.CreateInstance();
		instance.Protected = true;
		instance.Looping = true;
		instance.Volume = .5f;
		instanceLength = instance.Length.TotalSeconds;
		instance.Play();
	}

	public override void Shutdown()
	{
		instance.Dispose();
		sound.Dispose();
	}

	public override void Update()
	{
		App.Title = $"Audio {App.Width}x{App.Height} : {App.WidthInPixels}x{App.HeightInPixels}";

		if (Input.Keyboard.Pressed(Keys.Left))
		{
			instance.Cursor = TimeSpan.FromSeconds(Math.Clamp(instance.Cursor.TotalSeconds - 10, 0, instanceLength));
		}

		if (Input.Keyboard.Pressed(Keys.Right))
		{
			instance.Cursor = TimeSpan.FromSeconds(Math.Clamp(instance.Cursor.TotalSeconds + 10, 0, instanceLength));
		}

		if (Input.Keyboard.Pressed(Keys.Up))
		{
			instance.Pitch = Math.Clamp(instance.Pitch + .25f, 0, 4);
		}

		if (Input.Keyboard.Pressed(Keys.Down))
		{
			instance.Pitch = Math.Clamp(instance.Pitch - .25f, 0, 4);
		}

		if (Input.Keyboard.Pressed(Keys.Space))
		{
			if(instance.Playing)
			{
				instance.Pause();
			}
			else
			{
				instance.Play();
			}
		}

		if (Input.Keyboard.Pressed(Keys.S))
		{
			instance.Spatialized = !instance.Spatialized;
		}

		var x = Math.Sin(Time.Duration.TotalSeconds) * 20;
		var position = new Vector3((float)x, .1f, 0);
		instance.Velocity = position - lastPosition;
		instance.Position = position;
		lastPosition = position;
	}

	public override void Render()
	{
		Graphics.Clear(Color.Black);

		batch.PushMatrix(new Vector2(App.WidthInPixels, App.HeightInPixels) / 2 - PlayBarSize / 2);
		batch.Rect(new Rect(Vector2.Zero, PlayBarSize), Color.Gray);
		batch.Rect(new Rect((float)(PlayBarSize.X * instance.Cursor.TotalSeconds / instanceLength), PlayBarSize.Y), Color.Blue);
		batch.Text(font, $"{instance.Cursor:mm':'ss}/{TimeSpan.FromSeconds(instanceLength):mm':'ss}", new(PlayBarSize.X / 2, PlayBarSize.Y / 2), new(0.5f, 0.5f), Color.White);
		batch.Text(font, $"x{instance.Pitch}", new(PlayBarSize.X / 2, PlayBarSize.Y + 5), new(0.5f, 0f), Color.White);

		if(instance.Spatialized)
		{
			batch.Circle(new(instance.Position.X * 10 + PlayBarSize.X/2, -20), 5, 10, Color.Red);
		}

		batch.PopMatrix();

		batch.Render();
		batch.Clear();
	}
}

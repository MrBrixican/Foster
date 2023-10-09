namespace Foster.Framework.Audio;

public class Sound : IDisposable
{
	public int MaxInstances { get; set; } = int.MaxValue;
	public int ActiveInstances { get; internal set; }

	public string Path { get; private set; }

	private IntPtr data { get; set; }
	private SoundLoadingMethod SoundLoadingMethod { get; set; } // ?

	public Sound(string path, SoundLoadingMethod loadingMethod = SoundLoadingMethod.Preload)
	{
		Path = System.IO.Path.GetFullPath(path);
		if(loadingMethod is SoundLoadingMethod.Preload or SoundLoadingMethod.PreloadDecoded)
		{
			using var stream = File.OpenRead(Path);
			Load(stream, loadingMethod == SoundLoadingMethod.PreloadDecoded);
		}
	}

	public Sound(Stream stream, bool decode = false)
	{
		Path ??= Guid.NewGuid().ToString();
		Load(stream, decode);
	}

	private void Load(Stream stream, bool decode = false)
	{
		var data = new byte[stream.Length - stream.Position];
		stream.Read(data);
	}

	public SoundInstance Play()
	{
		var instance = CreateInstance();
		instance.Play();
		return instance;
	}
	public SoundInstance CreateInstance()
	{
		Audio.TryCreateSoundInstance(this, 0, out var instance);
		return instance;
	}

	public void PlayAll() { }

	public void PauseAll() { }

	public void StopAll() { }

	public void ApplyAll(Action<SoundInstance> action) { }

	~Sound() => Dispose();

	public void Dispose()
	{
		if (data != IntPtr.Zero)
		{
			// TODO
		}
	}
}

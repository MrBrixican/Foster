namespace Foster.Framework.Audio;

[Flags]
internal enum SoundInstanceCreateFlags : byte
{
	Streamed = 1 << 0,
	Spatialized = 1 << 1,
	Protected = 1 << 2,
}

namespace Foster.Framework.Audio;

// Maybe flags would be better? Not sure since Stream is not compatible with others
// Preload simply creates a special sound instance (not connected to engine nor cleaned up by Audio) to keep sound loaded
// Decoded suffix just keeps the decoded copy in memory
// Load on demand will load a file when the first active instance is created, and unload it when all active instances are recycled
// Stream streams from disk
public enum SoundLoadingMethod
{
	Preload,
	PreloadDecoded,
	LoadOnDemand,
	LoadOnDemandDecoded,
	Stream
}

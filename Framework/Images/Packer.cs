
namespace Foster.Framework;

/// <summary>
/// The Packer takes source image data and packs them into large texture pages that can then be used for Atlases
/// This is useful for sprite fonts, sprite sheets, etc.
/// </summary>
public class Packer
{
	/// <summary>
	/// A single packed Entry
	/// </summary>
	public struct Entry
	{
		/// <summary>
		/// ID used when generating
		/// </summary>
		public int ID;

		/// <summary>
		/// The Name of the Entry
		/// </summary>
		public string Name;

		/// <summary>
		/// The corresponding image page of the Entry
		/// </summary>
		public int Page;

		/// <summary>
		/// The Source Rectangle
		/// </summary>
		public RectInt Source;

		/// <summary>
		/// The Frame Rectangle. This is the size of the image before it was packed
		/// </summary>
		public RectInt Frame;

		public Entry(int id, string name, int page, RectInt source, RectInt frame)
		{
			ID = id;
			Name = name;
			Page = page;
			Source = source;
			Frame = frame;
		}
	}

	/// <summary>
	/// Stores the Packed result of the Packer
	/// </summary>
	public readonly struct Output
	{
		public readonly List<Image> Pages = new();
		public readonly List<Entry> Entries = new();
		public Output() { }
	}

	/// <summary>
	/// Whether to trim transparency from the source images
	/// </summary>
	public bool Trim = true;

	/// <summary>
	/// Maximum Texture Size. If the packed data is too large it will be split into multiple pages
	/// </summary>
	public int MaxSize = 8192;

	/// <summary>
	/// Image Padding
	/// </summary>
	public int Padding = 1;

	/// <summary>
	/// Power of Two
	/// </summary>
	public bool PowerOfTwo = false;

	/// <summary>
	/// This will check each image to see if it's a duplicate of an already packed image. 
	/// It will still add the entry, but not the duplicate image data.
	/// </summary>
	public bool CombineDuplicates = false;

	/// <summary>
	/// The total number of source images
	/// </summary>
	public int SourceImageCount => sources.Count;

	private struct Source
	{
		public int ID;
		public int Hash;
		public string Name;
		public RectInt Packed;
		public RectInt Frame;
		public int BufferIndex;
		public int BufferLength;
		public int DuplicateOf;
		public bool Empty => Packed.Width <= 0 || Packed.Height <= 0;

		public Source(int id, string name)
		{
			ID = id;
			Name = name;
		}
	}

	private int nextSourceID = 0;
	private readonly List<Source> sources = new();
	private Color[] sourceBuffer = new Color[32];
	private int sourceBufferIndex = 0;

	public void Add(string name, Image image)
	{
		Add(name, image.Width, image.Height, image.Data);
	}

	public void Add(string name, string path)
	{
		Add(name, new Image(path));
	}

	public void Add(string name, int width, int height, ReadOnlySpan<Color> pixels)
	{
		var source = new Source(++nextSourceID, name);
		int top = 0, left = 0, right = width, bottom = height;

		// trim
		if (Trim)
		{
			// TOP:
			for (int y = 0; y < height; y++)
				for (int x = 0, s = y * width; x < width; x++, s++)
					if (pixels[s].A > 0)
					{
						top = y;
						goto LEFT;
					}
				LEFT:
			for (int x = 0; x < width; x++)
				for (int y = top, s = x + y * width; y < height; y++, s += width)
					if (pixels[s].A > 0)
					{
						left = x;
						goto RIGHT;
					}
				RIGHT:
			for (int x = width - 1; x >= left; x--)
				for (int y = top, s = x + y * width; y < height; y++, s += width)
					if (pixels[s].A > 0)
					{
						right = x + 1;
						goto BOTTOM;
					}
				BOTTOM:
			for (int y = height - 1; y >= top; y--)
				for (int x = left, s = x + y * width; x < right; x++, s++)
					if (pixels[s].A > 0)
					{
						bottom = y + 1;
						goto END;
					}
				END:;
		}

		// determine sizes
		// there's a chance this image was empty in which case we have no width / height
		if (left <= right && top <= bottom)
		{
			if (CombineDuplicates)
			{
				source.Hash = 0;
				for (int x = left; x < right; x++)
					for (int y = top; y < bottom; y++)
						source.Hash = ((source.Hash << 5) + source.Hash) + (int)pixels[x + y * width].RGBA;

				for (int i = 0; i < sources.Count; i ++)
					if (sources[i].Hash == source.Hash)
					{
						source.DuplicateOf = sources[i].ID;
						break;
					}
			}

			source.Packed = new RectInt(0, 0, right - left, bottom - top);
			source.Frame = new RectInt(-left, -top, width, height);

			if (source.DuplicateOf == 0)
			{
				var append = source.Packed.Width * source.Packed.Height;
				while (sourceBufferIndex + append >= sourceBuffer.Length)
					Array.Resize(ref sourceBuffer, sourceBuffer.Length * 2);

				source.BufferIndex = sourceBufferIndex;
				source.BufferLength = append;

				// copy our trimmed pixel data to the main buffer
				for (int i = 0; i < source.Packed.Height; i++)
				{
					var len = source.Packed.Width;
					var srcIndex = left + (top + i) * width;
					var dstIndex = sourceBufferIndex;
					var srcData = pixels.Slice(srcIndex, len);
					var dstData = sourceBuffer.AsSpan(dstIndex, len);

					srcData.CopyTo(dstData);
					sourceBufferIndex += len;
				}
			}
		}
		else
		{
			source.Packed = new RectInt();
			source.Frame = new RectInt(0, 0, width, height);
		}

		sources.Add(source);
	}

	private struct PackingNode
	{
		public bool Used;
		public RectInt Rect;
		public unsafe PackingNode* Right;
		public unsafe PackingNode* Down;
	};

	public unsafe Output Pack()
	{
		Output result = new();

		// Nothing to pack
		if (sources.Count <= 0)
			return result;

		// sort the sources by size
		sources.Sort((a, b) => b.Packed.Width * b.Packed.Height - a.Packed.Width * a.Packed.Height);

		// make sure the largest isn't too large
		if (sources[0].Packed.Width > MaxSize || sources[0].Packed.Height > MaxSize)
			throw new Exception("Source image is larger than max atlas size");

		// TODO: why do we sometimes need more than source images * 3?
		// for safety I've just made it 4 ... but it should really only be 3?

		int nodeCount = sources.Count * 4;
		Span<PackingNode> buffer = (nodeCount <= 2000 ?
			stackalloc PackingNode[nodeCount] :
			new PackingNode[nodeCount]);

		var padding = Math.Max(0, Padding);

		// using pointer operations here was faster
		fixed (PackingNode* nodes = buffer)
		{
			int packed = 0, page = 0;
			while (packed < sources.Count)
			{
				if (sources[packed].Empty)
				{
					packed++;
					continue;
				}

				var from = packed;
				var nodePtr = nodes;
				var rootPtr = ResetNode(nodePtr++, 0, 0, sources[from].Packed.Width + padding, sources[from].Packed.Height + padding);

				while (packed < sources.Count)
				{
					if (sources[packed].Empty || sources[packed].DuplicateOf != 0)
					{
						packed++;
						continue;
					}

					int w = sources[packed].Packed.Width + padding;
					int h = sources[packed].Packed.Height + padding;
					var node = FindNode(rootPtr, w, h);

					// try to expand
					if (node == null)
					{
						bool canGrowDown = (w <= rootPtr->Rect.Width) && (rootPtr->Rect.Height + h < MaxSize);
						bool canGrowRight = (h <= rootPtr->Rect.Height) && (rootPtr->Rect.Width + w < MaxSize);
						bool shouldGrowRight = canGrowRight && (rootPtr->Rect.Height >= (rootPtr->Rect.Width + w));
						bool shouldGrowDown = canGrowDown && (rootPtr->Rect.Width >= (rootPtr->Rect.Height + h));

						if (canGrowDown || canGrowRight)
						{
							// grow right
							if (shouldGrowRight || (!shouldGrowDown && canGrowRight))
							{
								var next = ResetNode(nodePtr++, 0, 0, rootPtr->Rect.Width + w, rootPtr->Rect.Height);
								next->Used = true;
								next->Down = rootPtr;
								next->Right = node = ResetNode(nodePtr++, rootPtr->Rect.Width, 0, w, rootPtr->Rect.Height);
								rootPtr = next;
							}
							// grow down
							else
							{
								var next = ResetNode(nodePtr++, 0, 0, rootPtr->Rect.Width, rootPtr->Rect.Height + h);
								next->Used = true;
								next->Down = node = ResetNode(nodePtr++, 0, rootPtr->Rect.Height, rootPtr->Rect.Width, h);
								next->Right = rootPtr;
								rootPtr = next;
							}
						}
					}

					// doesn't fit in this page
					if (node == null)
						break;

					// add
					node->Used = true;
					node->Down = ResetNode(nodePtr++, node->Rect.X, node->Rect.Y + h, node->Rect.Width, node->Rect.Height - h);
					node->Right = ResetNode(nodePtr++, node->Rect.X + w, node->Rect.Y, node->Rect.Width - w, h);

					var it = sources[packed];
					it.Packed.X = node->Rect.X;
					it.Packed.Y = node->Rect.Y;
					sources[packed] = it;

					packed++;
				}

				// get page size
				int pageWidth, pageHeight;
				if (PowerOfTwo)
				{
					pageWidth = 2;
					pageHeight = 2;
					while (pageWidth < rootPtr->Rect.Width)
						pageWidth *= 2;
					while (pageHeight < rootPtr->Rect.Height)
						pageHeight *= 2;
				}
				else
				{
					pageWidth = rootPtr->Rect.Width;
					pageHeight = rootPtr->Rect.Height;
				}

				// create each page
				{
					var bmp = new Image(pageWidth, pageHeight);
					result.Pages.Add(bmp);

					// create each entry for this page and copy its image data
					for (int i = from; i < packed; i++)
					{
						var source = sources[i];

						// do not pack duplicate entries yet
						if (source.DuplicateOf != 0)
							continue;

						result.Entries.Add(new (source.ID, source.Name, page, source.Packed, source.Frame));

						if (source.Empty)
							continue;

						var data = sourceBuffer.AsSpan(source.BufferIndex, source.BufferLength);
						bmp.CopyPixels(data, source.Packed.Width, source.Packed.Height, source.Packed.Position);
					}
				}

				page++;
			}

		}

		// make sure duplicates have entries
		if (CombineDuplicates)
		{
			foreach (var source in sources)
			{
				if (source.DuplicateOf == 0)
					continue;

				foreach (var entry in result.Entries)
					if (entry.ID == source.DuplicateOf)
					{   
						result.Entries.Add(new (source.ID, source.Name, entry.Page, entry.Source, entry.Frame));
						break;
					}
			}
		}

		return result;

		static unsafe PackingNode* FindNode(PackingNode* root, int w, int h)
		{
			if (root->Used)
			{
				var r = FindNode(root->Right, w, h);
				return (r != null ? r : FindNode(root->Down, w, h));
			}
			else if (w <= root->Rect.Width && h <= root->Rect.Height)
			{
				return root;
			}

			return null;
		}

		static unsafe PackingNode* ResetNode(PackingNode* node, int x, int y, int w, int h)
		{
			node->Used = false;
			node->Rect = new RectInt(x, y, w, h);
			node->Right = null;
			node->Down = null;
			return node;
		}
	}

	/// <summary>
	/// Removes all source data and removes the Packed Output
	/// </summary>
	public void Clear()
	{
		sources.Clear();
		sourceBufferIndex = 0;
	}

}
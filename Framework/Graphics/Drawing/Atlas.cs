using System;
using System.Collections.Generic;

namespace Foster.Framework
{
    /// <summary>
    /// A Texture Atlas
    /// </summary>
    public class Atlas
    {
        /// <summary>
        /// List of all the Texture Pages of the Atlas
        /// Generally speaking it's ideal to have a single Page per atlas, but that's not always possible.
        /// </summary>
        public List<Texture> Pages { get; } = new List<Texture>();

        /// <summary>
        /// A Dictionary of all the TextureRegions in this Atlas.
        /// </summary>
        public Dictionary<string, TextureRegion> TextureRegions { get; } = new Dictionary<string, TextureRegion>();

        /// <summary>
        /// An empty Atlas
        /// </summary>
        public Atlas() { }

        /// <summary>
        /// An Atlas created from an Image Packer, optionally premultiplying the textures
        /// </summary>
        public Atlas(Packer packer, bool premultiply = false)
        {
            var output = packer.Pack();
            if (output != null)
            {
                foreach (var page in output.Pages)
                {
                    if (premultiply)
                    {
                        page.Premultiply();
                    }

                    Pages.Add(new Texture(page));
                }

                foreach (var entry in output.Entries.Values)
                {
                    var texture = Pages[entry.Page];
                    var textureRegion = new TextureRegion(texture, entry.Source, entry.Frame);

                    TextureRegions.Add(entry.Name, textureRegion);
                }
            }
        }

        /// <summary>
        /// Gets or Sets a TextureRegion by name
        /// </summary>
        public TextureRegion? this[string name]
        {
            get
            {
                if (TextureRegions.TryGetValue(name, out var subtex))
                {
                    return subtex;
                }

                return null;
            }
            set
            {
                if (value != null)
                {
                    TextureRegions[name] = value;
                }
            }
        }
    }
}

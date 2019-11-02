﻿
using Imgur.API.Enums;
using Imgur.API.JsonConverters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;

namespace Imgur.API.Models.Impl
{
  /// <summary>The data for albums.</summary>
  public class Album : IAlbum, IDataModel
  {
    /// <summary>The account ID or null if it's anonymous.</summary>
    [JsonProperty("account_id")]
    public virtual int? AccountId { get; set; }

    /// <summary>The account username or null if it's anonymous.</summary>
    [JsonProperty("account_url")]
    public virtual string AccountUrl { get; set; }

    /// <summary>The ID of the album cover image.</summary>
    public virtual string Cover { get; set; }

    /// <summary>The height, in pixels, of the album cover image.</summary>
    [JsonProperty("cover_height")]
    public virtual int? CoverHeight { get; set; }

    /// <summary>
    ///     True if the album has been submitted to the gallery, false if otherwise.
    /// </summary>
    [JsonProperty("in_gallery")]
    public virtual bool InGallery { get; set; }

    /// <summary>
    ///     Utc timestamp of when the album was inserted into the gallery, converted from epoch time.
    /// </summary>
    [JsonConverter(typeof (EpochTimeConverter))]
    public virtual DateTimeOffset DateTime { get; set; }

    /// <summary>
    ///     OPTIONAL, the deletehash, if you're logged in as the album owner.
    /// </summary>
    public virtual string DeleteHash { get; set; }

    /// <summary>The description of the album in the gallery.</summary>
    public virtual string Description { get; set; }

    /// <summary>
    ///     Indicates if the current user favorited the album. Defaults to false if not signed in.
    /// </summary>
    public virtual bool? Favorite { get; set; }

    /// <summary>The ID for the album.</summary>
    public virtual string Id { get; set; }

    /// <summary>The width, in pixels, of the album cover image.</summary>
    [JsonProperty("cover_width")]
    public virtual int? CoverWidth { get; set; }

    /// <summary>
    ///     A list of all the images in the album (only available when requesting the direct album).
    /// </summary>
    [JsonConverter(typeof (TypeConverter<IEnumerable<Image>>))]
    public virtual IEnumerable<IImage> Images { get; set; } = (IEnumerable<IImage>) new List<IImage>();

    /// <summary>The total number of images in the album.</summary>
    [JsonProperty("images_count")]
    public virtual int ImagesCount { get; set; }

    /// <summary>The view layout of the album.</summary>
    [JsonConverter(typeof (StringEnumConverter))]
    public virtual AlbumLayout? Layout { get; set; }

    /// <summary>The URL link to the album.</summary>
    public virtual string Link { get; set; }

    /// <summary>
    ///     Indicates if the image has been marked as nsfw or not. Defaults to null if information is not available.
    /// </summary>
    public virtual bool? Nsfw { get; set; }

    /// <summary>
    ///     Order number of the album on the user's album page (defaults to 0 if their albums haven't been reordered).
    /// </summary>
    public virtual int Order { get; set; }

    /// <summary>
    ///     The privacy level of the album, you can only view public virtual if not logged in as album owner.
    /// </summary>
    [JsonConverter(typeof (StringEnumConverter))]
    public virtual AlbumPrivacy? Privacy { get; set; }

    /// <summary>
    ///     If the image has been categorized then this will contain the section the image belongs in. (funny, cats,
    ///     adviceanimals, wtf, etc)
    /// </summary>
    public virtual string Section { get; set; }

    /// <summary>The title of the album in the gallery.</summary>
    public virtual string Title { get; set; }

    /// <summary>The number of album views.</summary>
    public virtual int Views { get; set; }
  }
}

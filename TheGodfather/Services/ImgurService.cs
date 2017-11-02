﻿#region USING_DIRECTIVES
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

using TheGodfather.Helpers;

using DSharpPlus.CommandsNext;

using Imgur.API;
using Imgur.API.Authentication.Impl;
using Imgur.API.Endpoints.Impl;
using Imgur.API.Enums;
using Imgur.API.Models.Impl;
using Imgur.API.Models;
#endregion

namespace TheGodfather.Services
{
    public class ImgurService
    {
        private ImgurClient _imgur = null;
        private GalleryEndpoint _endpoint = null;


        public ImgurService(string key)
        {
            _imgur = new ImgurClient(key);
            _endpoint = new GalleryEndpoint(_imgur);
        }


        public async Task<IEnumerable<IGalleryItem>> GetItemsFromSubAsync(string sub,
                                                                          int num,
                                                                          SubredditGallerySortOrder order,
                                                                          TimeWindow time)
        {

            var images = await _endpoint.GetSubredditGalleryAsync(sub, order, time)
                .ConfigureAwait(false);
            return images.Take(num);
        }
    }
}

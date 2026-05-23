using System;
using System.Collections.Generic;
using System.Text;

namespace Rift_App.Models
{
    public class GameInfoModel
    {
        public int AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "game";
        public string Description { get; set; } = string.Empty;
        public string HeaderImageUrl { get; set; } = string.Empty;
        public List<string> Tags { get; set; } = new();
        public List<string> Genres { get; set; } = new();
        public List<string> Screenshots { get; set; } = new();
        public List<string> Developers { get; set; } = new();
        public List<string> Publishers { get; set; } = new();
        public string ReleaseDate { get; set; } = string.Empty;
        public string ReviewDesc { get; set; } = string.Empty;
        public string ReviewCss { get; set; } = string.Empty;
        public string DeveloperDisplay => Developers.FirstOrDefault() ?? "";
        public string PublisherDisplay => Publishers.FirstOrDefault() ?? "";
        public string TagsDisplay => string.Join(" • ", Tags.Take(4));
    }
}
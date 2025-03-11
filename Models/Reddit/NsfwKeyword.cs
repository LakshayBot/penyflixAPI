using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace pentyflixApi.Models
{
    [Table("nsfw_keywords", Schema = "public")]
    public class NsfwKeyword
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Column("keyword")]
        public string Keyword { get; set; }
    }
}
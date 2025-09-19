using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SvatebniWeb.Web.Data.Models
{
    /// <summary>
    /// Reprezentuje jeden konkrétní svatební web vytvořený uživatelem.
    /// </summary>
    public class Wedding
    {
        /// <summary>
        /// Unikátní identifikátor svatebního webu.
        /// Primární klíč v databázi.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Unikátní textový identifikátor, který bude použit v URL.
        /// Např. "Trnkovi" pro URL https://svatebniweb.cz/Trnkovi.
        /// </summary>
        [Required]
        [StringLength(100)]
        public required string UrlSlug { get; set; }

        /// <summary>
        /// Hlavní nadpis, který se zobrazí na stránce svatebního webu.
        /// </summary>
        [Required]
        [StringLength(200)]
        public required string Title { get; set; }

        // --- Propojení na vlastníka (uživatele) ---

        /// <summary>
        /// Cizí klíč pro uživatele (ApplicationUser), který tento web vytvořil.
        /// </summary>
        [Required]
        public required string OwnerId { get; set; }

        /// <summary>
        /// Navigační vlastnost pro Entity Framework Core, která umožňuje
        /// snadno načíst informace o uživateli, který je vlastníkem.
        /// </summary>
        [ForeignKey(nameof(OwnerId))]
        public ApplicationUser? Owner { get; set; }
    }
}
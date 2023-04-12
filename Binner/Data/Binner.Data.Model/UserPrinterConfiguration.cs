﻿using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Binner.Model.IO.Printing;

namespace Binner.Data.Model
{
#if INITIALCREATE
    /// <summary>
    /// Stores user defined printer configurations
    /// </summary>
    public class UserPrinterConfiguration : IEntity, IUserData
    {
        /// <summary>
        /// Primary key
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int UserPrinterConfigurationId { get; set; }

        /// <summary>
        /// Associated user
        /// </summary>
        public int? UserId { get; set; }

        /// <summary>
        /// If using a remote printer, specify the address Url.
        /// Requires Binner print spooler
        /// </summary>
        public string? RemoteAddressUrl { get;set;}

        /// <summary>
        /// Full name of printer
        /// Default: Dymo LabelWriter 450
        /// </summary>
        public string PrinterName { get; set; } = "Dymo LabelWriter 450 Twin Turbo";

        /// <summary>
        /// Label model number
        /// Default: 30346
        /// </summary>
        public string PartLabelName { get; set; } = "30346"; // LW 1/2" x 1 7/8"

        /// <summary>
        /// Label paper source
        /// </summary>
        public LabelSource PartLabelSource { get; set; } = LabelSource.Auto;

        /// <summary>
        /// Creation date
        /// </summary>
        public DateTime DateCreatedUtc { get; set; }

        public DateTime DateModifiedUtc { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        public ICollection<UserPrinterTemplateConfiguration>? UserPrinterTemplateConfigurations { get; set; }
    }
#endif
}

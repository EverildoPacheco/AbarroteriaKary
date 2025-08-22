using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class CLIENTE
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string CLIENTE_ID { get; set; } = null!;

    [StringLength(250)]
    [Unicode(false)]
    public string? CLIENTE_NOTA { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string CREADO_POR { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA_CREACION { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? MODIFICADO_POR { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_MODIFICACION { get; set; }

    public bool ELIMINADO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? ELIMINADO_POR { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FECHA_ELIMINACION { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string ESTADO { get; set; } = null!;

    [ForeignKey("CLIENTE_ID")]
    [InverseProperty("CLIENTE")]
    public virtual PERSONA CLIENTENavigation { get; set; } = null!;

    [InverseProperty("CLIENTE")]
    public virtual ICollection<VENTA> VENTA { get; set; } = new List<VENTA>();
}

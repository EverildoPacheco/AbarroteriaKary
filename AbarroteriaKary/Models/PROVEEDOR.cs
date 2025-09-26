using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class PROVEEDOR
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PROVEEDOR_ID { get; set; } = null!;

    //public PERSONA PERSONA { get; set; } = default!;  // navegación (uno a uno, PK compartida)



    [StringLength(250)]
    [Unicode(false)]
    public string? PROVEEDOR_OBSERVACION { get; set; }

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

    [StringLength(100)]
    [Unicode(false)]
    public string? EMPRESA { get; set; }

    [InverseProperty("PROVEEDOR")]
    public virtual ICollection<PEDIDO> PEDIDO { get; set; } = new List<PEDIDO>();

    [ForeignKey("PROVEEDOR_ID")]
    [InverseProperty("PROVEEDOR")]
    public virtual PERSONA PROVEEDORNavigation { get; set; } = null!;
}

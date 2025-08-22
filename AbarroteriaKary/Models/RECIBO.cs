using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("VENTA_ID", Name = "IX_RECIBO_VENTA")]
public partial class RECIBO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string RECIBO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string VENTA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string METODO_PAGO_ID { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal MONTO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime FECHA { get; set; }

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

    [ForeignKey("METODO_PAGO_ID")]
    [InverseProperty("RECIBO")]
    public virtual METODO_PAGO METODO_PAGO { get; set; } = null!;

    [ForeignKey("VENTA_ID")]
    [InverseProperty("RECIBO")]
    public virtual VENTA VENTA { get; set; } = null!;
}

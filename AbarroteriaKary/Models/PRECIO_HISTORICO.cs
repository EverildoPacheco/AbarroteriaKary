using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("PRODUCTO_ID", "DESDE", Name = "IX_PRECIO_PRODUCTO_DESDE")]
public partial class PRECIO_HISTORICO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PRECIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal PRECIO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime DESDE { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? HASTA { get; set; }

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

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("PRECIO_HISTORICO")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}

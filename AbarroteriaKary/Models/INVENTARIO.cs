using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("FECHA_VENCIMIENTO", Name = "IX_INVENTARIO_FECHA_VENC")]
[Index("PRODUCTO_ID", "FECHA_VENCIMIENTO", Name = "IX_INV_PROD_FECHA")]
[Index("INVENTARIO_ID", "PRODUCTO_ID", Name = "UQ_INVENTARIO_ID_PROD", IsUnique = true)]
public partial class INVENTARIO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string INVENTARIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    public int STOCK_ACTUAL { get; set; }

    public int STOCK_MINIMO { get; set; }

    [Column(TypeName = "decimal(12, 2)")]
    public decimal COSTO_UNITARIO { get; set; }

    public DateOnly? FECHA_VENCIMIENTO { get; set; }

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

    [StringLength(50)]
    public string? LOTE_CODIGO { get; set; }

    [StringLength(300)]
    public string? MOTIVO { get; set; }

    [InverseProperty("INVENTARIO")]
    public virtual ICollection<DETALLE_VENTA> DETALLE_VENTA { get; set; } = new List<DETALLE_VENTA>();

    [InverseProperty("INVENTARIO")]
    public virtual ICollection<NOTIFICACION> NOTIFICACION { get; set; } = new List<NOTIFICACION>();

    [ForeignKey("PRODUCTO_ID")]
    [InverseProperty("INVENTARIO")]
    public virtual PRODUCTO PRODUCTO { get; set; } = null!;
}

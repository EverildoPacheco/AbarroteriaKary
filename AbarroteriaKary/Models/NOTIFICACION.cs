using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("INVENTARIO_ID", "PRODUCTO_ID", Name = "IX_NOTIF_INV_PROD")]
public partial class NOTIFICACION
{
    [Key]
    public int NOTIFICACION_ID { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string INVENTARIO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string PRODUCTO_ID { get; set; } = null!;

    [StringLength(15)]
    [Unicode(false)]
    public string TIPO { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string? MENSAJE { get; set; }

    public byte? NIVEL { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime FECHA_DETECCION { get; set; }

    public bool RESUELTA { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RESUELTA_EN { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? RESUELTA_POR { get; set; }

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

    [ForeignKey("INVENTARIO_ID, PRODUCTO_ID")]
    [InverseProperty("NOTIFICACION")]
    public virtual INVENTARIO INVENTARIO { get; set; } = null!;
}

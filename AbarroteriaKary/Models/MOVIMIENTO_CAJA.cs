using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class MOVIMIENTO_CAJA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string MOVIMIENTO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string SESION_ID { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime FECHA { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string TIPO { get; set; } = null!;

    [Column(TypeName = "decimal(12, 2)")]
    public decimal MONTO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? REFERENCIA { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? DESCRIPCION { get; set; }

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

    [ForeignKey("SESION_ID")]
    [InverseProperty("MOVIMIENTO_CAJA")]
    public virtual CAJA_SESION SESION { get; set; } = null!;
}

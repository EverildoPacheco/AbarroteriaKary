using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class BITACORA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string BITACORA_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string? USUARIO_ID { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string ACCION { get; set; } = null!;

    [StringLength(1000)]
    [Unicode(false)]
    public string? DETALLE { get; set; }

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

    [ForeignKey("USUARIO_ID")]
    [InverseProperty("BITACORA")]
    public virtual USUARIO? USUARIO { get; set; }
}

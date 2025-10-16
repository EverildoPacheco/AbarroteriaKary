using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

public partial class SUBMODULO
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string SUBMODULO_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string MODULO_ID { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string SUBMODULO_NOMBRE { get; set; } = null!;

    [StringLength(200)]
    [Unicode(false)]
    public string? SUBMODULO_RUTA { get; set; }

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

    [ForeignKey("MODULO_ID")]
    [InverseProperty("SUBMODULO")]
    public virtual MODULO MODULO { get; set; } = null!;

    [InverseProperty("SUBMODULO")]
    public virtual ICollection<PERMISOS> PERMISOS { get; set; } = new List<PERMISOS>();
}

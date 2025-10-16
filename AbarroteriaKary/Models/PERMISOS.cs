using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("MODULO_ID", Name = "IX_PERMISOS_MODULO")]
[Index("ROL_ID", Name = "IX_PERMISOS_ROL")]
public partial class PERMISOS
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PERMISOS_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string ROL_ID { get; set; } = null!;

    [StringLength(10)]
    [Unicode(false)]
    public string MODULO_ID { get; set; } = null!;

    public bool PUEDE_VER { get; set; }

    public bool PUEDE_CREAR { get; set; }

    public bool PUEDE_EDITAR { get; set; }

    public bool PUEDE_ELIMINAR { get; set; }

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

    [StringLength(10)]
    [Unicode(false)]
    public string? SUBMODULO_ID { get; set; }

    [ForeignKey("MODULO_ID")]
    [InverseProperty("PERMISOS")]
    public virtual MODULO MODULO { get; set; } = null!;

    [ForeignKey("ROL_ID")]
    [InverseProperty("PERMISOS")]
    public virtual ROL ROL { get; set; } = null!;

    [ForeignKey("SUBMODULO_ID")]
    [InverseProperty("PERMISOS")]
    public virtual SUBMODULO? SUBMODULO { get; set; }
}

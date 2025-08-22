using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AbarroteriaKary.Models;

[Index("PERSONA_CORREO", Name = "IX_PERSONA_CORREO")]
public partial class PERSONA
{
    [Key]
    [StringLength(10)]
    [Unicode(false)]
    public string PERSONA_ID { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string PERSONA_PRIMERNOMBRE { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string? PERSONA_SEGUNDONOMBRE { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? PERSONA_TERCERNOMBRE { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string PERSONA_PRIMERAPELLIDO { get; set; } = null!;

    [StringLength(50)]
    [Unicode(false)]
    public string? PERSONA_SEGUNDOAPELLIDO { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? PERSONA_APELLIDOCASADA { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? PERSONA_NIT { get; set; }

    [StringLength(13)]
    [Unicode(false)]
    public string? PERSONA_CUI { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string? PERSONA_DIRECCION { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? PERSONA_TELEFONOCASA { get; set; }

    [StringLength(15)]
    [Unicode(false)]
    public string? PERSONA_TELEFONOMOVIL { get; set; }

    [StringLength(150)]
    [Unicode(false)]
    public string? PERSONA_CORREO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PERSONA_FECHAREGISTRO { get; set; }

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

    [InverseProperty("CLIENTENavigation")]
    public virtual CLIENTE? CLIENTE { get; set; }

    [InverseProperty("EMPLEADONavigation")]
    public virtual EMPLEADO? EMPLEADO { get; set; }

    [InverseProperty("PROVEEDORNavigation")]
    public virtual PROVEEDOR? PROVEEDOR { get; set; }
}

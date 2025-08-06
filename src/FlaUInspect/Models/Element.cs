﻿using FlaUI.Core.Definitions;

namespace FlaUInspect.Models;

public class Element {
    public string? Name { get; set; }
    public string? AutomationId { get; set; }
    public ControlType ControlType { get; set; }
    public List<Element> Children { get; set; } = [];
}
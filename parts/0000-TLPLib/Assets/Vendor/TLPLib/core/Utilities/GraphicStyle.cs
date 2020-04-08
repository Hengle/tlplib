﻿using System;
using System.Collections.Generic;

using com.tinylabproductions.TLPLib.Components.gradient;
using com.tinylabproductions.TLPLib.Functional;
using UnityEngine;
using UnityEngine.UI;

namespace com.tinylabproductions.TLPLib.Utilities {
  [Serializable] public class GraphicStyle {
    public Color graphicColor, outlineColor;

    public bool gradient;
    bool gradientOn => gradient;
    /*[Inspect(nameof(gradientOn))] */public Color gradientColor;

    public GraphicStyle(Color graphicColor, bool gradient, Color gradientColor, Color outlineColor) {
      this.graphicColor = graphicColor;
      this.gradient = gradient;
      this.gradientColor = gradientColor;
      this.outlineColor = outlineColor;
    }

    public void applyStyle(List<Graphic> graphics) {
      foreach (var graphic in graphics) {
        applyStyle(graphic);
      }
    }

    public void applyStyle(Graphic graphic) {
      foreach (var _outline in F.opt(graphic.GetComponent<Shadow>())) {
        _outline.effectColor = outlineColor;
      }
      graphic.color = graphicColor;
      foreach (var grad in F.opt(graphic.GetComponent<GradientSimple>())) {
        if (gradientOn) {
          grad.enabled = true;
          grad.setColor(graphicColor, gradientColor);
        }
        else {
          grad.enabled = false;
        }
      }
    }
  }
}
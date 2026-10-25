$ErrorActionPreference='Stop'
$root=(Get-Location).Path
$tablePath='artifacts/validation/blob-s1-integration/compiled-field-table.json'
$table=Get-Content -LiteralPath $tablePath -Raw|ConvertFrom-Json
$parameter='tools/SunEyeVision.BlobAnalysis/BlobAnalysisParameters.cs'
$algorithm='tools/SunEyeVision.BlobAnalysis/BlobAnalysisTool.cs'
$renderer='src/Plugin.SDK/UI/Controls/ImageControl/ResultRenderLayer.cs'
function Unit($symbol,$meaning,$source,$status='KnownBusinessSemantic'){
 [ordered]@{symbol=$symbol;meaning=$meaning;source=$source;status=$status;scalarUnitEnforcedByContracts=$false;automaticConversion=$false}
}
$inputs=foreach($field in $table.fields){
 $key=$field.LogicalKey;$unit=Unit 'not-applicable' 'Boolean switch or closed symbolic choice; no physical unit' ($parameter+' / '+$field.LegacyPath)
 switch -Regex ($key){
  '^setting\.(Threshold|MaxValue)$' {$unit=Unit 'gray-level' 'Byte-image intensity level 0..255' ($parameter+' Threshold/MaxValue');break}
  '^setting\.(PreprocessKernelSize|MorphElementWidth|MorphElementHeight)$' {$unit=Unit 'px' 'Image-grid kernel/structuring-element span; not calibrated world length' ($algorithm+' ApplyPreprocessing/BuildStructuringElement');break}
  '^setting\.(MorphIterations|BlobCountLimit)$' {$unit=Unit 'count' 'Iteration count or retained Blob count' ($parameter+' / '+$field.LegacyPath);break}
  '^setting\.FilterArea' {$unit=Unit 'px²' 'Image-region area in pixel cells' ($algorithm+' SelectShape(area)/AreaCenter');break}
  '^setting\.Filter(Circularity|Rectangularity)' {$unit=Unit 'ratio' 'Dimensionless shape measure 0..1' ($algorithm+' Circularity/Rectangularity and clamp to 1');break}
  '^input\.pose$' {$unit=Unit 'mixed-record' 'Affine coefficients plus from/to frame labels; offsets use declared target coordinates, no invented calibration' 'src/Plugin.Contracts/SpatialDeclarations.cs Affine2D/ValidateAffine2D';break}
  '^input\.image$' {$unit=Unit 'image-resource' 'Physical frame, pixel format, stride and actual Domain accompany the resource' 'src/Plugin.Contracts/Resources.cs';break}
  '^input\.(inspection|mask)\.(local|geometrySource)$' {$unit=Unit 'mixed-ROI-record' 'center/vertices/radii/width/height: px; direction vector: dimensionless; angle-degrees: deg; start/end-radians: rad; kind/flags: no unit' 'src/Plugin.Contracts/RoiDeclarations.cs; src/Plugin.SDK/Execution/Parameters/RoiShapeValueAdapter.cs';break}
  '^input\.(inspection|mask)\.regionSource$' {$unit=Unit 'region-resource' 'Discrete region on its own image-grid frame, distinct from geometry and empty/Unset' 'src/Plugin.Contracts/Resources.cs';break}
  '^presentation\.judgments\.([^.]+)\.(Min|Max)$' {
   $judgment=$Matches[1]
   if($judgment -eq 'white-ratio'){$unit=Unit '%' 'White pixel count / full image pixel count * 100, range 0..100' ($algorithm+' EvaluateJudgments whitePixelCount / totalPixels * 100')}
   elseif($judgment -eq 'blob-count'){$unit=Unit 'count' 'Blob count threshold' ($parameter+' CreateResultPresentation(blob-count)')}
   elseif($judgment -match 'area'){$unit=Unit 'px²' 'Total/mean/min/max/per-item pixel-region area threshold' ($parameter+' CreateResultPresentation Unit=px²')}
   elseif($judgment -match 'circularity|rectangularity'){$unit=Unit 'ratio' 'Dimensionless shape-measure threshold 0..1' ($parameter+' CreateResultPresentation AllowedMaximum=1')}
   else{$unit=Unit 'unused-boolean-threshold' 'Boolean output-not-empty rule does not consume range Min/Max' ($parameter+' CreateResultPresentation RuleKind=Boolean')};break
  }
  '\.(OkColor|NgColor)$' {$unit=Unit 'UInt8-channel' 'RGBA record; each channel 0..255, distinct from opacity ratio' 'src/Plugin.Contracts/PresentationDeclarations.cs';break}
  '\.Opacity$' {$unit=Unit 'ratio' 'Drawing opacity 0..1; passed to PushOpacity, scalar unit is descriptive' 'src/Plugin.SDK/Models/Visualization/GraphicsStyle.cs; ResultRenderLayer.PushOpacity';break}
  '\.(LineWidth|CrossSize)$' {$unit=Unit 'viewport-render-length' 'Base rendering length transformed by viewport scaling; not measured image/world length' ($renderer+' GetScaledLineWidth/ViewportScaling.MarkerPower');break}
  '\.FontSize$' {$unit=Unit 'WPF-em-size' 'FormattedText em size, DPI-aware and viewport-scaled; not image/world length' ($renderer+' CreateFormattedText/ApplyTextTransforms');break}
  '\.(PositionX|PositionY)$' {$unit=Unit 'unconfirmed-display-anchor' 'Stored text anchor coordinate; exact image/screen frame contract not independently established in this S1 package' 'src/Plugin.SDK/Models/Visualization/TextStyle.cs PositionX/PositionY' 'Unconfirmed';break}
 }
 [ordered]@{fieldId=$field.Port.Id;logicalKey=$key;legacyPath=$field.LegacyPath;storage=$field.Port.Storage;cardinality=$field.Port.Type.Count;businessUnit=$unit;typeFingerprint=$field.Port.Type.Fingerprint}
}
$outputNames=@('ExecutionTimeMs','ExecutionStatusText','JudgmentStatus','IsPass','GrayImage','BinaryMask','OverlayImage','ThresholdMinUsed','ThresholdMaxUsed','TypeUsed','FilteredBlobCount','FilteredTotalArea','AverageArea','AverageCircularity','AverageRectangularity','WhitePixelRatio','HistogramData','ChannelMeans','BlobAreas','BlobPerimeters','BlobCentroidXs','BlobCentroidYs','BlobCircularities','BlobRectangularities','BlobBoundingWidths','BlobBoundingHeights','BlobBoundingRects','InspectionGeometries','MaskGeometries')
$outputs=foreach($port in $table.outputs){
 $id=$port.Id;$index=[int]($id.Substring($id.Length-12));$name=if($index -eq 100){'FilteredBlobs'}else{$outputNames[$index-1]}
 $unit=Unit 'not-applicable' 'Status/choice/Boolean output; no physical unit' 'tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs FieldId'
 switch -Regex ($name){
  '^ExecutionTimeMs$' {$unit=Unit 'ms' 'Elapsed execution time in milliseconds' 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs ExecutionTimeMs';break}
  'Image$|^BinaryMask$' {$unit=Unit 'image-resource' 'Image resource with frame/pixel format/actual Domain' 'src/Plugin.Contracts/Resources.cs';break}
  '^Threshold' {$unit=Unit 'gray-level' 'Byte-image threshold used, 0..255' ($algorithm+' threshold range');break}
  '^FilteredBlobCount$|^HistogramData$' {$unit=Unit 'count' 'Blob count, or histogram frequency per intensity bin' ($algorithm+' histogram/count');break}
  '^FilteredTotalArea$|^AverageArea$|^BlobAreas$' {$unit=Unit 'px²' 'Region area, scalar or per-Blob sequence' ($algorithm+' AreaCenter');break}
  'Circularit|Rectangularit' {$unit=Unit 'ratio' 'Dimensionless shape measure 0..1' ($algorithm+' Circularity/Rectangularity');break}
  '^WhitePixelRatio$' {$unit=Unit '%' 'White count / full image pixel count *100; not 0..1' ($algorithm+' EvaluateJudgments');break}
  '^ChannelMeans$' {$unit=Unit 'input-intensity' 'Per-input-channel mean; byte input channel intensity' ($algorithm+' intensity statistics');break}
  '^Blob(Perimeters|CentroidXs|CentroidYs|BoundingWidths|BoundingHeights)$' {$unit=Unit 'px' 'Image-grid perimeter/coordinate/length, per Blob' 'tools/SunEyeVision.BlobAnalysis/BlobResult.cs';break}
  '^BlobBoundingRects$' {$unit=Unit 'formatted-px' 'Presentation strings of width x height, not a numeric conversion contract' 'tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs BlobBoundingRects';break}
  'Geometries$' {$unit=Unit 'mixed-ROI-record' 'Image-pixel ROI with explicit degree/radian members as in input ROI' 'src/Plugin.Contracts/RoiDeclarations.cs';break}
  '^FilteredBlobs$' {$unit=Unit 'mixed-blob-record' 'area px²; perimeter/centroid/bounds px; circularity/rectangularity ratio; IDs/count/flags no physical unit; geometry pixel frame' 'tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs BlobType/Freeze';break}
 }
 [ordered]@{fieldId=$id;legacyName=$name;storage=$port.Storage;cardinality=$port.Type.Count;businessUnit=$unit;typeFingerprint=$port.Type.Fingerprint}
}
if(@($inputs).Count -ne 117 -or @($outputs).Count -ne 30){throw 'Unit table field coverage mismatch'}
$sources=@($parameter,$algorithm,$renderer,'src/Plugin.Contracts/RoiDeclarations.cs','src/Plugin.Contracts/SpatialDeclarations.cs','src/Plugin.Contracts/PresentationDeclarations.cs','src/Plugin.SDK/Execution/Parameters/RoiShapeValueAdapter.cs','src/Plugin.SDK/Models/Visualization/GraphicsStyle.cs','src/Plugin.SDK/Models/Visualization/TextStyle.cs','tools/SunEyeVision.BlobAnalysis/BlobOutputContract.cs','tools/SunEyeVision.BlobAnalysis/BlobAnalysisResults.cs','tools/SunEyeVision.BlobAnalysis/BlobResult.cs')|ForEach-Object{[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_).Hash}}
[ordered]@{purpose='Descriptive business units from existing source; not a runtime unit conversion framework';compiledFieldTableSha=(Get-FileHash $tablePath).Hash;inputs=@($inputs);outputs=@($outputs);sources=@($sources)}|ConvertTo-Json -Depth 18|Set-Content artifacts/validation/blob-s1-integration/business-unit-table.json -Encoding utf8

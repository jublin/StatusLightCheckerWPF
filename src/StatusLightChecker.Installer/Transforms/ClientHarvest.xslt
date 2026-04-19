<?xml version="1.0" encoding="UTF-8"?>
<!--
  Strips appsettings.Development.json from the harvested client output.
  The production appsettings.json is kept and installed to the binary directory.
-->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:wix="http://wixtoolset.org/schemas/v4/wxs">

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="wix:Component[wix:File[contains(@Source, 'appsettings.Development.json')]]" />

</xsl:stylesheet>

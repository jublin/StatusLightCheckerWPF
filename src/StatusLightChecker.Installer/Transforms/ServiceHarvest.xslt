<?xml version="1.0" encoding="UTF-8"?>
<!--
  Two jobs:
  1. Strip appsettings.Development.json — not needed in production.
     The production appsettings.json is kept and installed to the binary directory.
  2. Inject ServiceInstall + ServiceControl into the StatusLightChecker.Service.exe
     component so the Windows Service is registered with that exe as its image path.
     Wait="no" on ServiceControl prevents MSI rollback if the service fails to start
     immediately (e.g. Teams is not running at install time).
-->
<xsl:stylesheet version="1.0"
    xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:wix="http://wixtoolset.org/schemas/v4/wxs"
    xmlns:util="http://wixtoolset.org/schemas/v4/wxs/util">

  <xsl:template match="node()|@*">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*" />
    </xsl:copy>
  </xsl:template>

  <!-- Strip development config -->
  <xsl:template match="wix:Component[wix:File[contains(@Source, 'appsettings.Development.json')]]" />

  <!-- Inject service registration into the service exe component -->
  <xsl:template match="wix:Component[wix:File[contains(@Source, 'StatusLightChecker.Service.exe')]]">
    <xsl:copy>
      <xsl:apply-templates select="@*|node()" />
      <util:ServiceInstall
        Name="StatusLightCheckerService"
        DisplayName="Status Light Checker Service"
        Description="Monitors application status for LED light control"
        Type="ownProcess"
        Start="auto"
        ErrorControl="normal"
        Account="LocalSystem" />
      <util:ServiceControl
        Name="StatusLightCheckerService"
        Start="install"
        Stop="both"
        Remove="uninstall"
        Wait="no" />
    </xsl:copy>
  </xsl:template>

</xsl:stylesheet>

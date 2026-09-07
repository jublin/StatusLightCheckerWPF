# Existing layout
The legacy MainWindow is a 1200x800 service dashboard, with sidebar, app bar, service controls, current status, history. The NEW companion has no rendered UI yet. Its portable compact shell replaces this layout; it does not reproduce it.

```xml
<Window x:Class="StatusLightChecker.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:materialDesign="http://materialdesigninxaml.net/winfx/xaml/themes"
        xmlns:vm="clr-namespace:StatusLightChecker.ViewModels"
        mc:Ignorable="d"
        Title="Status Light Checker" 
        Height="800" Width="1200"
        TextElement.Foreground="{DynamicResource MaterialDesignBody}"
        Background="{DynamicResource MaterialDesignPaper}"
        FontFamily="{materialDesign:MaterialDesignFont}"
        d:DataContext="{d:DesignInstance vm:MainViewModel, IsDesignTimeCreatable=False}"
    WindowStartupLocation="CenterScreen">
    
    <materialDesign:DialogHost Identifier="RootDialog" DialogTheme="Inherit">
        <materialDesign:DrawerHost IsLeftDrawerOpen="{Binding ElementName=MenuToggleButton, Path=IsChecked}">
            
            <materialDesign:DrawerHost.LeftDrawerContent>
                <StackPanel Width="220">
                    <!-- Header -->
                    <StackPanel Margin="16,24,16,16">
                        <Ellipse Width="64" Height="64">
                            <Ellipse.Fill>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                                    <GradientStop Color="{DynamicResource PrimaryHueMidColor}" Offset="0" />
                                    <GradientStop Color="{DynamicResource SecondaryHueMidColor}" Offset="1" />
                                </LinearGradientBrush>
                            </Ellipse.Fill>
                        </Ellipse>
                        
                        <TextBlock Text="Status Checker" 
                                  Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                  Margin="0,16,0,0"/>
                        
                        <TextBlock Text="v2.0.0" 
                                  Style="{StaticResource MaterialDesignBody2TextBlock}"
                                  Opacity="0.6"/>
                    </StackPanel>
                    
                    <Separator Margin="16,0"/>
                    
                    <!-- Navigation -->
                    <ListBox Margin="0,8,0,0">
                        <ListBoxItem IsSelected="True">
                            <StackPanel Orientation="Horizontal" Margin="8,4">
                                <materialDesign:PackIcon Kind="ViewDashboard" 
                                                          Margin="0,0,16,0"
                                                          VerticalAlignment="Center"/>
                                <TextBlock Text="Dashboard" 
                                          Style="{StaticResource MaterialDesignBody1TextBlock}">
                                </TextBlock>
                            </StackPanel>
                        </ListBoxItem>
                        
                        <ListBoxItem x:Name="SettingsNavItem">
                            <StackPanel Orientation="Horizontal" Margin="8,4">
                                <materialDesign:PackIcon Kind="Cog" 
                                                          Margin="0,0,16,0"
                                                          VerticalAlignment="Center"/>
                                <TextBlock Text="Settings" 
                                          Style="{StaticResource MaterialDesignBody1TextBlock}">
                                </TextBlock>
                            </StackPanel>
                        </ListBoxItem>
                    </ListBox>
                    
                </StackPanel>
            </materialDesign:DrawerHost.LeftDrawerContent>
            
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                
                <!-- App Bar -->
                <materialDesign:ColorZone Grid.Row="0"
                                           Mode="PrimaryMid"
                                           Padding="16">
                    <Grid>
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto"/>
                            <ColumnDefinition Width="*"/>
                            <ColumnDefinition Width="Auto"/>
                        </Grid.ColumnDefinitions>
                        
                        <ToggleButton Grid.Column="0"
                                      Style="{StaticResource MaterialDesignHamburgerToggleButton}"
                                      x:Name="MenuToggleButton"
                                      IsChecked="False"/>
                        
                        <TextBlock Grid.Column="1"
                                   Text="Status Light Checker"
                                   Style="{StaticResource MaterialDesignHeadline6TextBlock}"
                                   VerticalAlignment="Center"
                                   Margin="16,0,0,0"/>
                        
                        <StackPanel Grid.Column="2" 
                                      Orientation="Horizontal">
                            <!-- Connection Status -->
                            <materialDesign:Chip Content="{Binding ConnectionStatus}"
                                               Margin="8,0"
                                               Background="{DynamicResource MaterialDesignPaper}">
                                <materialDesign:Chip.Icon>
                                    <materialDesign:PackIcon Kind="LanConnect"
                                                               Foreground="{DynamicResource PrimaryHueMidBrush}"
                                                               Visibility="{Binding IsConnected, Converter={StaticResource BooleanToVisibilityConverter}}"/>
                                </materialDesign:Chip.Icon>
                            </materialDesign:Chip>
                            
                            <!-- Service Status -->
                            <materialDesign:Chip Content="{Binding ServiceStatus}"
                                               Margin="8,0"
                                               Background="{Binding ServiceStatusColor}">
                                <materialDesign:Chip.Icon>
                                    <materialDesign:PackIcon Kind="Circle"
                                                               Foreground="White"/>
                                </materialDesign:Chip.Icon>
                            </materialDesign:Chip>
                            
                        </StackPanel>
                    </Grid>
                </materialDesign:ColorZone>
                
                <!-- Main Content -->
                <ScrollViewer Grid.Row="1" Margin="16">
                    <StackPanel>
                        
                        <!-- Service Control Card -->
                        <materialDesign:Card Margin="0,0,0,16"
                                             Style="{StaticResource MaterialDesignOutlinedCard}">
                            <Grid Margin="16">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                
                                <StackPanel Grid.Column="0">
                                    <TextBlock Text="Service Control"
                                              Style="{StaticResource MaterialDesignHeadline6TextBlock}">
                                    </TextBlock>
                                    
                                    <TextBlock Text="Control the background Windows Service"
                                              Style="{StaticResource MaterialDesignBody2TextBlock}"
                                              Opacity="0.6"
                                              Margin="0,4,0,0">
                                    </TextBlock>
                                </StackPanel>
                                
                                <StackPanel Grid.Column="1" 
                                              Orientation="Horizontal">
                                    <Button Style="{StaticResource MaterialDesignOutlinedButton}"
                                            Content="START"
                                            Command="{Binding StartServiceCommand}"
                                            IsEnabled="{Binding CanControlService}"
                                            Visibility="{Binding ServiceStatus, Converter={StaticResource ServiceStatusToVisibilityConverter}, ConverterParameter=Stopped}"
                                            Margin="0,0,8,0"/>
                                    
                                    
                                    <Button Style="{StaticResource MaterialDesignOutlinedButton}"
                                            Content="STOP"
                                            Command="{Binding StopServiceCommand}"
                                            IsEnabled="{Binding CanControlService}"
                                            Visibility="{Binding ServiceStatus, Converter={StaticResource ServiceStatusToVisibilityConverter}, ConverterParameter=Running}"
                                            Margin="0,0,8,0"/>
                                    
                                    
                                    <Button Style="{StaticResource MaterialDesignIconButton}"
                                            ToolTip="Restart Service"
                                            Command="{Binding RestartServiceCommand}"
                                            IsEnabled="{Binding CanControlService}">
                                        <materialDesign:PackIcon Kind="Restart" />
                                    </Button>
                                    
                                    
                                    <Button Style="{StaticResource MaterialDesignIconButton}"
                                            ToolTip="Settings"
                                            Command="{Binding OpenSettingsCommand}">
                                        <materialDesign:PackIcon Kind="Cog" />
                                    </Button>
                                    
                                </StackPanel>
                            </Grid>
                        </materialDesign:Card>
                        
                        <!-- Current Status Card -->
                        <materialDesign:Card Margin="0,0,0,16"
                                             Style="{StaticResource MaterialDesignOutlinedCard}">
                            <Grid Margin="16">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="*"/>
                                </Grid.RowDefinitions>
                                
                                <StackPanel Grid.Row="0" Margin="0,0,0,16">
                                    <TextBlock Text="Current Status"
                                              Style="{StaticResource MaterialDesignHeadline6TextBlock}">
                                    </TextBlock>
                                </StackPanel>
                                
                                <Grid Grid.Row="1">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="Auto"/>
                                        <ColumnDefinition Width="*"/>
                                    </Grid.ColumnDefinitions>
                                    
                                    <Grid Grid.Column="0" Margin="0,0,24,0">
                                        <Ellipse Width="80" Height="80">
                                            <Ellipse.Fill>
                                                <Binding Path="CurrentStatus.StatusLevel">
                                                    <Binding.Converter>
                                                        <StaticResource ResourceKey="StatusLevelToBrushConverter"/>
                                                    </Binding.Converter>
                                                </Binding>
                                            </Ellipse.Fill>
                                        </Ellipse>
                                        
                                        <materialDesign:PackIcon Width="40" Height="40"
                                                                  HorizontalAlignment="Center"
                                                                  VerticalAlignment="Center"
                                                                  Foreground="White"
                                                                  Kind="{Binding CurrentStatus.StatusLevel, Converter={StaticResource StatusLevelToIconConverter}}"/>
                                    </Grid>
                                    
                                    <StackPanel Grid.Column="1" VerticalAlignment="Center">
                                        <TextBlock Text="{Binding CurrentStatus.StatusLabel, FallbackValue=Unknown}"
                                                  Style="{StaticResource MaterialDesignHeadline4TextBlock}">
                                        </TextBlock>
                                        
                                        <TextBlock Text="{Binding CurrentStatus.ApplicationName, FallbackValue=Waiting for data...}"
                                                  Style="{StaticResource MaterialDesignBody1TextBlock}"
                                                  Opacity="0.7"
                                                  Margin="0,4,0,0">
                                        </TextBlock>
                                    </StackPanel>
                                </Grid>
                            </Grid>
                        </materialDesign:Card>
                        
                        <!-- Status History -->
                        <materialDesign:Card Style="{StaticResource MaterialDesignOutlinedCard}">
                            <Grid Margin="16">
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="*"/>
                                </Grid.RowDefinitions>
                                
                                <StackPanel Grid.Row="0" Margin="0,0,0,16">
                                    <TextBlock Text="Status History"
                                              Style="{StaticResource MaterialDesignHeadline6TextBlock}">
                                    </TextBlock>
                                </StackPanel>
                                
                                <ItemsControl Grid.Row="1"
                                                ItemsSource="{Binding StatusHistory}">
                                    <ItemsControl.ItemsPanel>
                                        <ItemsPanelTemplate>
                                            <StackPanel/>
                                        </ItemsPanelTemplate>
                                    </ItemsControl.ItemsPanel>
                                    
                                    <ItemsControl.ItemTemplate>
                                        <DataTemplate>
                                            <materialDesign:Card Margin="0,0,0,8"
                                                                Style="{StaticResource MaterialDesignOutlinedCard}">
                                                
                                                <Grid Margin="12">
                                                    
                                                    <Grid.ColumnDefinitions>
                                                        <ColumnDefinition Width="Auto"/>
                                                        <ColumnDefinition Width="*"/>
                                                        <ColumnDefinition Width="Auto"/>
                                                    </Grid.ColumnDefinitions>
                                                    
                                                    <Ellipse Grid.Column="0" 
                                                             Width="24" Height="24"
                                                             Fill="{Binding StatusBrush}"
                                                             Margin="0,0,12,0">
                                                    </Ellipse>
                                                    
                                                    <StackPanel Grid.Column="1">
                                                        <TextBlock Text="{Binding StatusLabel}"
                                                                  Style="{StaticResource MaterialDesignBody1TextBlock}"
                                                                  FontWeight="SemiBold">
                                                        </TextBlock>
                                                        
                                                        <TextBlock Text="{Binding ApplicationName}"
                                                                  Style="{StaticResource MaterialDesignCaptionTextBlock}"
                                                                  Opacity="0.6">
                                                        </TextBlock>
                                                    </StackPanel>
                                                    
                                                    <TextBlock Grid.Column="2"
                                                              Text="{Binding Timestamp, StringFormat={}{0:HH:mm:ss}}"
                                                              Style="{StaticResource MaterialDesignCaptionTextBlock}"
                                                              Opacity="0.5">
                                                    </TextBlock>
                                                    
                                                </Grid>                                            
                                            </materialDesign:Card>
                                        </DataTemplate>
                                    </ItemsControl.ItemTemplate>
                                </ItemsControl>
                            </Grid>
                        </materialDesign:Card>
                        
                    </StackPanel>
                </ScrollViewer>
                
            </Grid>
        </materialDesign:DrawerHost>
    </materialDesign:DialogHost>
</Window>
```

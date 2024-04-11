using ArcGIS.Desktop.Framework.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.IO;
using ArcGIS.Desktop.Mapping;
using ArcGIS.Core.Data;
using ArcGIS.Desktop.Core.Geoprocessing;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using static System.Net.Mime.MediaTypeNames;
using System.Security.Policy;
using ArcGIS.Desktop.Internal.Mapping.Symbology;
using ArcGIS.Desktop.Internal.Mapping.Object3DRenderingFilter;
using ArcGIS.Desktop.Internal.Mapping.Controls.TextExpressionBuilder;
using ArcGIS.Desktop.Core;
using ArcGIS.Desktop.Internal.Layouts.Utilities;
using ArcGIS.Core.Geometry;
using ArcGIS.Desktop.Internal.Mapping.Events;
using ArcGIS.Desktop.Editing;
using System.Runtime.CompilerServices;
using ArcGIS.Desktop.Internal.Mapping.TOC;

namespace projektpls
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {

        private string path = @"H:\";
        private string demPath = @"H:\";
        private string waterPath = @"H:\";
        private string urbanPath = @"H:\";
        private string naturePath = @"H:\";
        private string slopePath = @"H:\";
        private string aspectPath = @"H:\";
        private string heightPath = @"H:\";
        private string windPath = @"H:\";
        private string roadPath = @"H:\";
        private string rutnatPath = @"H:\";

        private int heightConstraint = 40;
        private int highConstraint = 100;
        private int lowConstraint = 10;
        private int natureConstraint = 50;
        private int waterConstraint = 100;
        private int urbanConstraint = 100;
        private int roadConstraint = 30;

        private string OpenFileExplorer(string initialDirectory, string filter)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = initialDirectory;
            openFileDialog.Filter = filter;

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedFilePath = openFileDialog.FileName;
                return selectedFilePath;
            }
            return @"H:\";
        }
        private void setPath(string path)
        {
            this.path = System.IO.Path.GetFullPath(path);
        }
        public Window1()
        {
            InitializeComponent();
            FillcmbAspect();
        }

        private void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if(rutnatPath.Equals(@"H:\"))
            {
                MessageBox.Show("Ange rutnät tack!");
                return;
            } 
            else if(roadPath.Equals(@"H:\"))
            {
                MessageBox.Show("Ange vägar tack!");
                return;
            }
            CalculateSlope();
            
            CalculateAspect();
            
            CalculateHeight(heightConstraint);

            CalculateBuffer(natureConstraint, naturePath);
            CalculateBuffer(waterConstraint, waterPath);
            CalculateBuffer(urbanConstraint, urbanPath);
            CalculateBuffer(roadConstraint, roadPath);

            EraseBuffer(naturePath, "nature");
            EraseBuffer(waterPath, "water");
            EraseBuffer(roadPath, "roads");
            EraseBuffer(urbanPath, "urban");

            BufferToRaster(naturePath, "nature");
            BufferToRaster(waterPath, "water");
            BufferToRaster(roadPath, "roads");
            BufferToRaster(urbanPath, "urban");

            CalculateConstraint(naturePath, "nature");
            CalculateConstraint(waterPath, "water");
            CalculateConstraint(roadPath, "roads");
            CalculateConstraint(urbanPath, "urban");

            performMCA();
        }


        private async void CalculateBuffer(int constraint, string path)
        {
            if(path.Equals(@"H:\"))
            {
                return;
            }
            string output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "_buffer.shp");
            if (naturePath.Equals(path))
            {
                naturePath = output;
            }
            else if (waterPath.Equals(path))
            {
                waterPath = output;
            }
            else if (urbanPath.Equals(path))
            {
                urbanPath = output;
            }
            else if (roadPath.Equals(path))
            {
                roadPath = output;
            }
            await QueuedTask.Run(() =>
            {
                // Steg 1: Gör en buffer på shape filen
                var parameters = Geoprocessing.MakeValueArray(path, output, $"{constraint} Meters");
                Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null, null, null, GPExecuteToolFlags.Default);
            });
        }
        private async void CalculateHeight(int constraint)
        {
            if (demPath.Equals(@"H:\"))
            {
                return;
            }
            string outputHeight = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "Height.tif");
            string expression = $"Con((\"{demPath}\" < {constraint}), 1, 0)";
            heightPath = outputHeight;
            await QueuedTask.Run(() =>
            {
                // Steg 1: Beräkna höjddata
                var parameters = Geoprocessing.MakeValueArray(expression, outputHeight);
                Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", parameters);
                
            });
        }
        private async void CalculateSlope()
        {
            if (demPath.Equals(@"H:\") || roadPath.Equals(@"H:\"))
            {
                return;
            }
            string outputSlope = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "Slope.tif");
            string outputFilteredSlope = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "SlopeFiltered.tif");
            slopePath = outputFilteredSlope;
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Steg 1: Beräkna sluttningen
                    var parameters = Geoprocessing.MakeValueArray(demPath, outputSlope, "DEGREE", 1);
                    Geoprocessing.ExecuteToolAsync("3d.Slope", parameters, null, null, null, GPExecuteToolFlags.Default);
                    // Steg 2: Avgränsa datan
                    string expression = $"Con((\"{outputSlope}\" <= {highConstraint}) & (\"{outputSlope}\" >= {lowConstraint}), 1, 0)";
                    parameters = Geoprocessing.MakeValueArray(expression, outputFilteredSlope);
                    Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", parameters);
                    slopePath = outputFilteredSlope;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error calculating slope: " + ex.Message);
            }
        }
        private async void CalculateAspect()
        {
            if (demPath.Equals(@"H:\"))
            {
                return;
            }
            string outputAspect = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "Aspect.tif");
            string outputFilteredAspect = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "FilteredAspect.tif");
            aspectPath = outputFilteredAspect;
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Steg 1: Skapa en fil med alla vindriktningar
                    var parameters = Geoprocessing.MakeValueArray(demPath, outputAspect);
                    Geoprocessing.ExecuteToolAsync("3d.Aspect", parameters, null, null, null, GPExecuteToolFlags.Default);
                    // Steg 2: Skapa en fil med bara den bestämda vindriktningen
                    Dictionary<string, string> aspectExpressions = new Dictionary<string, string>()
                    {
                        { "North", $"Con((\"{outputAspect}\" > 0) & (\"{outputAspect}\" < 22.5) | (\"{outputAspect}\" > 337.5) & (\"{outputAspect}\" < 360), 1, 0)" },
                        { "Northeast", $"Con((\"{outputAspect}\" > 22.5) & (\"{outputAspect}\" < 67.5), 1, 0)" },
                        { "East", $"Con((\"{outputAspect}\" > 67.5) & (\"{outputAspect}\" < 112.5), 1, 0)" },
                        { "Southeast", $"Con((\"{outputAspect}\" > 112.5) & (\"{outputAspect}\" < 157.5), 1, 0)" },
                        { "South", $"Con((\"{outputAspect}\" > 157.5) & (\"{outputAspect}\" < 202.5), 1, 0)" },
                        { "Southwest", $"Con((\"{outputAspect}\" > 202.5) & (\"{outputAspect}\" < 247.5), 1, 0)" },
                        { "West", $"Con((\"{outputAspect}\" > 247.5) & (\"{outputAspect}\" < 292.5), 1, 0)" },
                        { "Northwest", $"Con((\"{outputAspect}\" > 292.5) & (\"{outputAspect}\" < 337.5), 1, 0)" }
                    };
                    string expression = $"Con((\"{outputAspect}\" > 247.5) & (\"{outputAspect}\" < 292.5), 1, 0)";
                    string selectedAspect = "";
                    Dispatcher.Invoke(() =>
                    {
                        selectedAspect = cmbAspect.Text;
                    });
                    if (aspectExpressions.ContainsKey(selectedAspect))
                    {
                        expression = aspectExpressions[selectedAspect];
                    }
                    parameters = Geoprocessing.MakeValueArray(expression, outputFilteredAspect);
                    Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", parameters);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error calculating aspect: " + ex.Message);
            }
            aspectPath = outputFilteredAspect;
        }
        
        private void EraseBuffer(string input, string category)
        {
            if (input.Equals(@"H:\"))
            {
                return;
            }
            string bufferpath = input;
            string path = Directory.GetCurrentDirectory();
            string output = path + "\\" + category + "erased.shp";
            if (naturePath.Equals(input))
            {
                naturePath = output;
            }
            else if (waterPath.Equals(input))
            {
                waterPath = output;
            }
            else if (urbanPath.Equals(input))
            {
                urbanPath = output;
            }
            else if (roadPath.Equals(input))
            {
                roadPath = output;
            }

            QueuedTask.Run(() =>
            {
                
                var parameters = Geoprocessing.MakeValueArray(rutnatPath, bufferpath, output);
                var gpErase = Geoprocessing.ExecuteToolAsync("Analysis.erase", parameters);
                
                if (gpErase.Result.IsFailed)
                {
                    MessageBox.Show("erase calculation failed.", "Error");
                    return;
                }
            });
        }

        public void BufferToRaster(string path, string category)
        {
            if (path.Equals(@"H:\"))
            {
                return;
            }
            string dirPath = Directory.GetCurrentDirectory();
            string outputRaster = dirPath + "\\" + category + "BufferToRaster.tif";
            if (category.Equals("nature"))
            {
                naturePath = outputRaster;

            }
            else if (category.Equals("water"))
            {
                waterPath = outputRaster;
            }
            else if (category.Equals("urban"))
            {
                urbanPath = outputRaster;
            }
            else if (category.Equals("roads"))
            {
                roadPath = outputRaster;
            }
            QueuedTask.Run(() =>
            {
                var parameters = Geoprocessing.MakeValueArray(path, null, outputRaster);
                var gpConversion = Geoprocessing.ExecuteToolAsync("conversion.PolygonToRaster", parameters);

                if (gpConversion.Result.IsFailed)
                {
                    MessageBox.Show("Buffer to raster calculation failed.", "Error");
                    return;
                }
            });
        }
        private async void CalculateConstraint(string inRaster, string category)
        {
            if (inRaster.Equals(@"H:\"))
            {
                return;
            }
            Map map = MapView.Active.Map;
            string filepath = inRaster;
            string dirPath = Directory.GetCurrentDirectory();
            string outRaster = dirPath + "\\" + category + "Final.tif";
            if (category.Equals("nature"))
            {
                naturePath = outRaster;

            }
            else if (category.Equals("water"))
            {
                waterPath = outRaster;
            }
            else if (category.Equals("urban"))
            {
                urbanPath = outRaster;
            }
            else if (category.Equals("roads"))
            {
                roadPath = outRaster;
            }

            await QueuedTask.Run(() =>
            {
                
                RasterLayer rasterLayer = LayerFactory.Instance.CreateLayer(new Uri(filepath), map) as RasterLayer;
           
                var raster = rasterLayer.GetRaster();

            
                var bandnameArray = new string[raster.GetBandCount()];
                for (int i = 0; i < raster.GetBandCount(); i++)
                {
                    var rasterBand = raster.GetBand(i);
                
                    var rasterBandName = raster.GetBand(i).GetName();

              
                    bandnameArray[i] = filepath + "\\" + rasterBandName;
                }

                string maExpression = string.Empty;
                if (category.Equals("roads"))
                {
                    maExpression = $"Con(IsNull(\"{bandnameArray[0]}\"), 1, 0)";
                }
                else
                {
                    maExpression = $"Con(IsNull(\"{bandnameArray[0]}\"), 0, 1)";
                }

                var valueArray = Geoprocessing.MakeValueArray(maExpression, outRaster);

       
                var gpToolCalc = Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", valueArray);

     
                if (gpToolCalc.Result.IsFailed)
                {
                 
                    var errors = gpToolCalc.Result.ErrorMessages.Select(err => err.Text);
             
                    MessageBox.Show("Error executing constraint Calculator: " + string.Join(Environment.NewLine, errors));
                }
                else
                {
 
                }



            });
        }
        private async void performMCA()
        {
            string path = Directory.GetCurrentDirectory();
            string maExpression = $"Int(\"{roadPath}\")";
            if (!demPath.Equals(@"H:\"))
            {
                //maExpression = $"Int(\"{roadPath}\") * Int(\"{waterPath}\") * Int(\"{urbanPath}\") * Int(\"{naturePath}\") * Int(\"{aspectPath}\") * Int(\"{heightPath}\") * Int(\"{slopePath}\")";
                maExpression += $" * Int(\"{aspectPath}\") * Int(\"{heightPath}\")* Int(\"{slopePath}\")";
            }
            if (!waterPath.Equals(@"H:\"))
            {
                maExpression += $" * Int(\"{waterPath}\")";
            }
            if (!urbanPath.Equals(@"H:\"))
            {
                maExpression += $" * Int(\"{urbanPath}\")";
            }
            if (!naturePath.Equals(@"H:\"))
            {
                maExpression += $" * Int(\"{naturePath}\")";
            }
            await QueuedTask.Run(() =>
            {
                string outRaster = path + "\\MCA.tif";

                var valueArray = Geoprocessing.MakeValueArray(maExpression, outRaster);
                // execute the Raster calculator tool to process the map algebra expression
                var gpTool = Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", valueArray);
                ConvertFinalRasterToPolygon(outRaster);
            });
        }
        private async void ConvertFinalRasterToPolygon(string inRaster)
        {
            string intermediateShapefilePath = Directory.GetCurrentDirectory() + "\\intermediate_output_polygon.shp";
            string finalShapefilePath = Directory.GetCurrentDirectory() + "\\final_filtered_polygon.shp";

            await QueuedTask.Run(async () =>
            {
               
                var parameters = Geoprocessing.MakeValueArray(inRaster, intermediateShapefilePath, "NO_SIMPLIFY", "VALUE");
                var gpResult = await Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters);

                if (gpResult.IsFailed)
                {
                    MessageBox.Show("Omvandling från raster till polygon misslyckades." + inRaster, "Fel");
                    return;
                }

                
                var selectParameters = Geoprocessing.MakeValueArray(intermediateShapefilePath, finalShapefilePath, "\"gridcode\" = 1");
                var selectResult = await Geoprocessing.ExecuteToolAsync("Select_analysis", selectParameters);

                if (selectResult.IsFailed)
                {
                    MessageBox.Show("Filtrering av polygon baserat på gridcode misslyckades.", "Fel");
                }

 
            });
        }



        private void FillcmbAspect()
        {
            cmbAspect.Items.Add("North");
            cmbAspect.Items.Add("Northeast");
            cmbAspect.Items.Add("East");
            cmbAspect.Items.Add("Southeast");
            cmbAspect.Items.Add("South");
            cmbAspect.Items.Add("Southwest");
            cmbAspect.Items.Add("West");
            cmbAspect.Items.Add("Northwest");
            cmbAspect.SelectedIndex = 0;
        }
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            txtDEM.Text = OpenFileExplorer(path, "TIFF files (*.tif)|*.tif|All files (*.*)|*.*");
        }
        private void tbxFilePath_TextChanged(object sender, TextChangedEventArgs e)
        {
            demPath = txtDEM.Text;
            setPath(demPath);
        }

        private void txtWater_TextChanged(object sender, TextChangedEventArgs e)
        {
            waterPath = txtWater.Text;
            setPath(waterPath);
        }

        private void btnWater_Click(object sender, RoutedEventArgs e)
        {
            txtWater.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }

        private void txtUrban_TextChanged(object sender, TextChangedEventArgs e)
        {
            urbanPath = txtUrban.Text;
            setPath(urbanPath);
        }

        private void btnUrban_Click(object sender, RoutedEventArgs e)
        {
            txtUrban.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }
        private void txtNature_TextChanged(object sender, TextChangedEventArgs e)
        {
            naturePath = txtNature.Text;
            setPath(naturePath);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            txtNature.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }

        private void txtMaxHeight_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtMaxHeight.Text, out int result))
                highConstraint = result;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtMinHeight.Text, out int result))
                lowConstraint = result;
        }

        private void txtHeight_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtHeight.Text, out int result))
                heightConstraint = result;
        }

        private void txtNatureBuffer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtNatureBuffer.Text, out int result))
                natureConstraint = result;
        }

        private void txtWaterBuffer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtWaterBuffer.Text, out int result))
                waterConstraint = result;
        }

        private void btnRoad_Click(object sender, RoutedEventArgs e)
        {
            txtRoad.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }

        private void btnWind_Click(object sender, RoutedEventArgs e)
        {
            txtWind.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }

        private void txtWind_TextChanged(object sender, TextChangedEventArgs e)
        {
            windPath = txtWind.Text;
            setPath(windPath);
        }

        private void txtRoadBuffer_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(txtRoadBuffer.Text, out int result))
                roadConstraint = result;
        }

        private void txtRoad_TextChanged(object sender, TextChangedEventArgs e)
        {
            roadPath = txtRoad.Text;
            setPath(roadPath);
        }

        private void btnRutnat_Click(object sender, RoutedEventArgs e)
        {
            txtRutnat.Text = OpenFileExplorer(path, "SHAPE files (*.shp)|*.shp|All files (*.*)|*.*");
        }

        private void txtRutnat_TextChanged(object sender, TextChangedEventArgs e)
        {
            rutnatPath = txtRutnat.Text;
            setPath(rutnatPath);
        }
    }
}

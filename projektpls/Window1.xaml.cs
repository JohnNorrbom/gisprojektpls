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
        private int heightConstraint = 40;
        private int highConstraint = 100;
        private int lowConstraint = 10;
        private int natureConstraint = 50;
        private int waterConstraint = 100;
        private int urbanConstraint = 100;
        private int roadConstraint = 30;
        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Allow only numeric characters (0-9) and the decimal point (.)
            if (!IsNumeric(e.Text))
            {
                e.Handled = true; // Prevent the character from being entered
            }
        }

        private bool IsNumeric(string text)
        {
            foreach (char c in text)
            {
                if (!char.IsDigit(c) && c != '.')
                {
                    return false;
                }
            }
            return true;
        }


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
            CalculateSlope();
            CalculateAspect();
            CalculateHeight(heightConstraint);
            CalculateBuffer(natureConstraint, naturePath);
            CalculateBuffer(waterConstraint, waterPath);
            CalculateBuffer(urbanConstraint, urbanPath);
            CalculateBuffer(roadConstraint, roadPath);
            PolygonToRaster(naturePath);
            PolygonToRaster(waterPath);
            PolygonToRaster(urbanPath);
        }
        private async void CalculateBuffer(int constraint, string path)
        {
            string output = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "_buffer.shp");
            await QueuedTask.Run(() =>
            {
                // Steg 1: Gör en buffer på shape filen
                var parameters = Geoprocessing.MakeValueArray(path, output, $"{constraint} Meters");
                Geoprocessing.ExecuteToolAsync("Buffer_analysis", parameters, null, null, null, GPExecuteToolFlags.Default);
            });
            if(naturePath.Equals(path))
            {
                naturePath = output;
            } 
            else if (waterPath.Equals(path))
            {
                waterPath = output;
            } 
            else if(urbanPath.Equals(path))
            {
                urbanPath = output;
            }
        }
        private async void CalculateHeight(int constraint)
        {
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

            string outputSlope = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "Slope.tif");
            string outputFilteredSlope = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(demPath), "SlopeFiltered.tif");
            slopePath = outputFilteredSlope;
            try
            {
                await QueuedTask.Run(() =>
                {
                    // Steg 1: Beräkns sluttningen
                    var parameters = Geoprocessing.MakeValueArray(demPath, outputSlope, "DEGREE", 1);
                    Geoprocessing.ExecuteToolAsync("3d.Slope", parameters, null, null, null, GPExecuteToolFlags.Default);
                    // Steg 2: Avgränsa datan
                    string expression = $"Con((\"{outputSlope}\" <= {highConstraint}) & (\"{outputSlope}\" >= {lowConstraint}), 1, 0)";
                    parameters = Geoprocessing.MakeValueArray(expression, outputFilteredSlope);
                    Geoprocessing.ExecuteToolAsync("RasterCalculator_sa", parameters);
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error calculating slope: " + ex.Message);
            }
        }
        private async void CalculateAspect()
        {
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
        }
        private async void RasterToPolygon(string path)
        {
            Map map = MapView.Active.Map;
            string outputPolygon = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "Polygon.shp");
            await QueuedTask.Run(() =>
            {
                // Steg 1: Konverterar från raster till polygon
                //r parameters = Geoprocessing.MakeValueArray(path, outputPolygon, "NO_SIMPLIFY", "Value");
                var parameters = Geoprocessing.MakeValueArray(path, outputPolygon, "NO_SIMPLIFY", "\"gridcode\" = 1");
                Geoprocessing.ExecuteToolAsync("RasterToPolygon_conversion", parameters, null, null, null, GPExecuteToolFlags.Default);
                var firstLayer = MapView.Active.Map.GetLayersAsFlattenedList()[0] as FeatureLayer;
                // Steg 2: Ta bort alla polygoner med värdet 0
                // Find the output polygon layer
                var outputLayer = map.GetLayersAsFlattenedList().OfType<FeatureLayer>().FirstOrDefault(layer => layer.GetFeatureClass().GetName().Equals(System.IO.Path.GetFileNameWithoutExtension(outputPolygon)));
                if (outputLayer != null)
                {
                    var delOp = new EditOperation()
                    {
                        Name = "Delete polygons with Value 0"
                    };
                    string fieldName = "gridcode";
                    var queryFilter = new QueryFilter()
                    {
                        WhereClause = $"{fieldName} = 0"
                    };
                    // Select polygons with Value = 0
                    var selection = outputLayer.Select(queryFilter);
                    // Assuming we have a selection on the layer...
                    delOp.Delete(outputLayer, selection.GetObjectIDs());
                    // Execute the edit operation asynchronously
                    delOp.ExecuteAsync(); // You can await it here if necessary
                }
                else
                {
                    MessageBox.Show("Failed to convert raster to polygon or the result is invalid.");
                }
            });
        }
        private async void PolygonToRaster(string path) 
        {
            string outputRaster = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "Raster.tif");
            path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), System.IO.Path.GetFileNameWithoutExtension(path) + "_buffer.shp");

            await QueuedTask.Run(() =>
            {

                var expression = $"Con((\"{outputRaster}\" == null), 1,0)";
                var parameters = Geoprocessing.MakeValueArray(expression, null,outputRaster);
                Geoprocessing.ExecuteToolAsync("PolygonToRaster_conversion", parameters, null, null, null, GPExecuteToolFlags.Default);
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
    }
}

/*
 * To change this license header, choose License Headers in Project Properties.
 * To change this template file, choose Tools | Templates
 * and open the template in the editor.
 */
package hawkeye;

import hawkeye.dialogs.FirstTimeDialog;
import hawkeye.dialogs.SimpleDialog;
import hawkeye.dialogs.SplashDialog;
import hawkeye.data.ArchiveDataImporter;
import hawkeye.data.DataFeature;
import hawkeye.data.database.HawkeyeDB;
import hawkeye.data.database.HawkeyeDB.DatabaseMode;
import hawkeye.data.objects.Image;
import hawkeye.data.objects.Score;
import hawkeye.documents.DocumentFeature;
import hawkeye.util.data.StringHelper;
import hawkeye.util.io.FileHelper;
import hawkeye.util.io.Unrar;
import java.io.File;
import java.io.FileInputStream;
import java.io.IOException;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import javafx.application.Application;
import javafx.concurrent.Task;
import javafx.scene.Parent;
import javafx.scene.paint.Color;
import javafx.stage.FileChooser;
import javafx.stage.Stage;

/**
 *
 * @author rnagel
 */
public class Hawkeye extends Application
{
    
    public static void testCode()
    {
        
    }
    
    @Override
    public void start(Stage primaryStage)
    {
                        
        // Very first thing we do is initialize settings
        // from settings.ini; it may contain default
        // startup values (among other things):
        initSettings();                   

        // Next, initialize some misc settings that have
        // to do with libraries and stuff:
        initMisc();

        // Show the "First Time" dialog if the program hasn't been run before.
        boolean firstTime = Settings.getBool("first_time_user", true);
        if (firstTime)
        {
            // If used, FirstTimeDialog will call up the SplashDialog.            
            FirstTimeDialog.showNew();
        }                
        
        // Show a splash dialog instance:
        // Beside presenting the title, it also allows user to select
        // the database mode to use for this session.
        SplashDialog splash = SplashDialog.showNew(primaryStage);        
        
        // Initialize some things before showing the main GUI:
        initEnvironment();
        initDatabase(splash.DatabaseMode, splash.CopySessionToPersistent, splash.ResetDatabase);                
        Score.setActiveScheme(Settings.getStr("score_active_scheme", "HWI 7-way"));
        String title = "Hawkeye (" + splash.DatabaseMode.name().charAt(0) + (splash.CopySessionToPersistent ? "+" : "") + ")";
        HawkeyeMainUI.showNew(title);
    }  
    
    /**
     * @param args the command line arguments
     */
    public static void main(String[] args)
    {
        launch(args);
    }

    // Data-enabled features that should be flagged when the dataset is updated (database or query change):
    private final static ArrayList<DataFeature> dataFeatures = new ArrayList<>();
    // Data-enabled features that are currently flagged as needing a data refresh:
    private final static ArrayList<DataFeature> flaggedDataFeatures = new ArrayList<>();
    
    // Session default document indexes:
    private final static HashMap<Class<? extends DocumentFeature>, Integer> defaultDocumentIndexes = new HashMap<>();
    
    // Loaded application graphics:
    private final static HashMap<String, javafx.scene.image.Image> graphics = new HashMap<>();
    
    // Application settings:
    public static final Settings Settings = new Settings();
    
    // OS
    public static String Platform = null;
    
    // Important paths/locations:
    public static File ExecutionPath = null;
    public static String ProjectTempFolder = null;
    public static String ArchiveTempFolder = null;    
        
    // Main GUI:
    public static Stage MainWindow = null;
    public static HawkeyeMainUI MainUI = null;
    
    // Hawkeye DB
    public static HawkeyeDB Database = null;
    public static HawkeyeDB CopyDatabase = null;        
    
    public static void initSettings()
    {
        Settings.initFromFile(new File("res/settings.ini"));
    }
    
    public static void initMisc()
    {
    }
    
	// Initialize some variables about the environment
    public static void initEnvironment()
    {        
        appLog("Initializing environment info...");
        // Which OS are we using?
        
        String OSName = System.getProperty("os.name").toUpperCase();
        String dataModel = System.getProperty("sun.arch.data.model").toUpperCase();
                
        if (OSName.contains("WINDOWS")) {
            if (dataModel.equals("32")) {
                Platform = "Win32";
            }
            else if (dataModel.equals("64")) {
                Platform = "Win64";
            }
            ProjectTempFolder = System.getProperty("java.io.tmpdir") + "Hawkeye_Temp_" + System.getProperty("user.name") + "\\Project_Temp";
            ArchiveTempFolder = System.getProperty("java.io.tmpdir") + "Hawkeye_Temp_" + System.getProperty("user.name") + "\\Archive_Temp";
        }
        else if (OSName.contains("LINUX")) {
            Platform = "Linux";
            ProjectTempFolder = System.getProperty("java.io.tmpdir") + "/Hawkeye_Temp_" + System.getProperty("user.name") + "/Project_Temp";
            ArchiveTempFolder = System.getProperty("java.io.tmpdir") + "/Hawkeye_Temp_" + System.getProperty("user.name") + "/Archive_Temp";
        }
        else if (OSName.contains("MAC")) {
            Platform = "Mac";
            ProjectTempFolder = System.getProperty("java.io.tmpdir") + "/Hawkeye_Temp_" + System.getProperty("user.name") + "/Project_Temp";
            ArchiveTempFolder = System.getProperty("java.io.tmpdir") + "/Hawkeye_Temp_" + System.getProperty("user.name") + "/Archive_Temp";
        }
        appLog("Platform: " + Platform);
        
        // Create temp folders, if they don't already exist:
        
        File ptemp = new File(ProjectTempFolder);
        ptemp.mkdirs();
        File atemp = new File(ArchiveTempFolder);
        atemp.mkdirs();

        // Get the execution path:        
        String path1 = Hawkeye.class.getClassLoader().getResource("hawkeye").getPath().replace("%20", " ");
        String path2 = path1.replaceFirst("file:", "");
        String path3;
        if (path1.contains("!")) {
            path3 = path2.substring(0, path2.lastIndexOf("!"));
        }
        else {
            path3 = path2.substring(0, path2.lastIndexOf("/classes/hawkeye"));
        }
        ExecutionPath = new File(path3);    
        
        appLog("Project_Temp: " + ProjectTempFolder + " exists: " + ptemp.exists());
        appLog("Archive_Temp: " + ArchiveTempFolder + " exists: " + atemp.exists());
    }
    
	// Create, start, and prepare the embedded database, optionally resetting it to "factory" (empty) condition.
    public static void initDatabase(boolean resetDatabase)
    {
        Database = new HawkeyeDB();
        Database.openConnection();
        Database.createDatabase(resetDatabase);
        Database.prepareStatements();
    }        
    
	// Pause execution on this thread for the specified number of milliseconds.
    public static void delay(long milliseconds)
    {
        try
        {
            Thread.sleep(milliseconds);
        }
        catch (InterruptedException ex)
        {            
        }
    }    
    
	// Called when the user chooses to exit the program.
    public static void userExit(Object pointOfExit)
    {
        deleteTempContent();
        Settings.saveToFile();
        appLog("User exited application from " + pointOfExit.getClass().getName());
        System.exit(0);
    }
    
	// Deletes temporary files resulting from the extraction of image archives (RARs) and project files (ZIPs).
    public static void deleteTempContent()
    {
        // Delete Archive Temp:
        File temp = new File(ArchiveTempFolder);
        for(File f: temp.listFiles()) 
            FileHelper.deletePath(f);
        appLog("Archive temp files deleted.");
        
        // Delete Project Temp:
        temp = new File(ProjectTempFolder);
        for(File f: temp.listFiles()) 
            FileHelper.deletePath(f);
        appLog("Project temp files deleted.");
    }
    
	// Writes the message to System.out IF logging is enabled.
    public static void appLog(String message)
    {
        if (Settings.getBool("use_app_log", Boolean.FALSE))
        {
            System.out.println("LOG-> " + message);
        }
    }
    
	// Writes a titled message to System.out or to a standard popup window.
    public static void appMessage(String message, String title, boolean toConsole, Parent parent)
    {
        if (toConsole)
        {
            String line = StringHelper.createRepeatedString("-", message.length());
            System.out.println("\n" + line);
            System.out.println(message);
            System.out.println(line);
        }
        if (parent != null)
        {         
            SimpleDialog.showMessage(parent.getScene().getWindow(), message, (title == null ? "Hawkeye Message" : title));
        }
    }
    
	// Standardized method for reporting application exceptions and errors.
    public static void appError(String errorMessage, Exception ex, boolean toConsole, Parent parent)
    {
        if (toConsole)
        {
            String line = StringHelper.createRepeatedString("#", errorMessage.length());
            System.out.println("\n" + line);
            System.out.println(errorMessage);
            if (ex != null)
            {
                System.out.println(ex.getMessage());
                System.out.println(ex.getStackTrace()[0]);
            }
            System.out.println(line);
        }
        if (parent != null)
        {
            String title = (ex != null ? ex.getClass().getSimpleName() : "Hawkeye Error");
            SimpleDialog.showMessage(parent.getScene().getWindow(), errorMessage + (ex != null ? "\n" + ex.getMessage() : ""), title);            
        }
    }
    
	// Returns the image with the specified filename, storing it in memory if it is not stored already.
    public static javafx.scene.image.Image getGraphic(String filename)
    {
        javafx.scene.image.Image graphic = graphics.get(filename);
        if (graphic != null)
        {
            return graphic;
        }
        try
        {
            graphic = new javafx.scene.image.Image(new FileInputStream("res/graphics/" + filename));
            graphics.put(filename, graphic);
            return graphic;
        }
        catch (IOException ex)
        {
            System.err.println("Graphic '" + filename + "' could not be loaded:\n" + ex.getMessage());
            return null;
        }                
    }
    
	// Returns the next index for an auto-named document of the specified type.
    public static int getNextIndexForDocumentType(Class documentType)
    {
        Integer value = defaultDocumentIndexes.get(documentType);
        if (value == null)
        {
            value = 0;
        }
        value++;
        defaultDocumentIndexes.put(documentType, value);
        return value;
    }
    
    // Flag all registered DataFeatures to be data-refreshed the next time they receive focus.
    public static void flagDataRefresh()
    {
        for (int d = 0; d < dataFeatures.size(); d++)
        {
            if (!flaggedDataFeatures.contains(dataFeatures.get(d)))
            {
                flaggedDataFeatures.add(dataFeatures.get(d));
            }            
        }
    }
	
	// Flag all registered DataFeatures to be refreshed, EXCPET for those specified.
    public static void flagDataRefreshExceptFor(DataFeature... dfs)
    {
        List<DataFeature> excludeList = Arrays.asList(dfs);        
        for (int d = 0; d < dataFeatures.size(); d++)
        {
            if (!flaggedDataFeatures.contains(dataFeatures.get(d)))
            {
                if (!excludeList.contains(dataFeatures.get(d)))
                    flaggedDataFeatures.add(dataFeatures.get(d));
            }            
        }
    }
    // Register a DataFeature so that it can be included in mass-flagging.
    public static void registerDataFeature(DataFeature df)
    {
        dataFeatures.add(df);        
    }
    // unregisterDataFeature() should be called when the features is permanently closed.
    // This will prevent refresh requests from being sent to it, and also elminate the
    // registration reference, making garbage collection more likely to remove the object from memory.
    public static void unregisterDataFeature(DataFeature df)
    {
        dataFeatures.remove(df);
        flaggedDataFeatures.remove(df);
    }    
    // Always call this to check if a DataFeature is flagged before performing a data refresh!!!
    public static boolean isDataFeatureFlagged(DataFeature df)
    {
        return flaggedDataFeatures.contains(df);
    }
    // refreshDataFeature() should be called in 3 cases:
    // 1. When a DocumentWindow containing a DataFeature receives focus
    // 2. When the MainUI window receives focus, and the currently selected tab contains a DataFeature
    // 3. When the tab selection changes to a tab that contains a DataFeature
    public static void refreshDataFeature(DataFeature df)
    {
        flaggedDataFeatures.remove(df);
        df.refreshData();        
    }
    
    // Returns the name of a RAR file without .rar extension.
    public static String getPackageName(String rarFilename)
    {
        return rarFilename.substring(0, rarFilename.lastIndexOf("."));
    }
    
	// Returns the temporary folder to which a RAR archive with the specified name will be extracted.
    public static File getExtractedArchiveFolder(String rarFilename)
    {
        return new File(ArchiveTempFolder + System.getProperty("file.separator") + getPackageName(rarFilename));
    }
    
	// Returns the path to which an experiment image with the specified name will be extracted.
    public static File getImageFilepath(Image image)
    {
        if (Settings.getBool("use_image_repository", Boolean.FALSE))
        {
            File plateFolder = new File(Settings.getStr("image_repository_location", null), image.ExperimentId.split("\\.")[0]);
            return new File(plateFolder, image.ImageFilename);
        }
        else
        {
            File archiveFolder = getExtractedArchiveFolder(image.ArchiveName);
            return new File(archiveFolder, image.ImageFilename);
        }
    }
    
	// Copies the specified experiment image file to the image repository.
    public static File copyToRepository(File imageFile)
    {
        try {
            String plateName = imageFile.getName().substring(0, 10);
            File plateFolder = new File(Settings.getStr("image_repository_location", null), plateName);
            if (!plateFolder.exists())
            {
                plateFolder.mkdir();
            }
            File newFile = new File(plateFolder, imageFile.getName());
            
            if (!newFile.exists())
            {
                Files.copy(imageFile.toPath(), newFile.toPath());
            }
            return newFile;
        } catch (IOException ex) {
            appError("'" + imageFile.getName() + "' could not be copied to the Image Repository.", ex, true, null);
            return null;
        }
    }
    
	// Opens a dialog allowing the user to choose multiple RAR archives to open.
    public static void openRarArchives()
    {
        FileChooser fileChooser = new FileChooser();
        fileChooser.setSelectedExtensionFilter(new FileChooser.ExtensionFilter("RAR Archives", "*.rar"));
        fileChooser.setTitle("Select RAR Archive(s) to import:");
        List<File> rars = fileChooser.showOpenMultipleDialog(MainWindow);
        List<String> skippedRars = new ArrayList<>();
        if (rars != null)
        {
            for (int r = 0; r < rars.size(); r++)
            {
                if (Database.checkArchiveExists(rars.get(r).getName()))
                {
                    skippedRars.add(rars.get(r).getName());
                }                
                else
                {
                    importRarArchive(rars.get(r));
                }
            }
            if (!skippedRars.isEmpty())
            {
                StringBuilder rarList = new StringBuilder();
                for (String rar:skippedRars)
                    rarList.append("\n- ").append(rar);
                appMessage("The following archives were not loaded because they already exist in the database:" + rarList.toString(), "Archive(s) Skipped", true, MainUI);
            }
        }
    }
    
	// Imports the contents of the specified RAR archive (data + images) into the database / image repository.
    public static void importRarArchive(File rarFile)
    {        
        String pkgName = rarFile.getName().substring(0, rarFile.getName().lastIndexOf("."));
        String tempPath = ArchiveTempFolder + "/" + pkgName;                        
        final File tempFolder = new File(tempPath);        

        File resultFiles[] = tempFolder.listFiles();        
        if (resultFiles == null)
        {
            tempFolder.mkdir();
            resultFiles = new File[0];
        }
        else if (resultFiles.length > 0)
        {
            for (File f: resultFiles)
            {
                f.delete();
            }
            resultFiles = tempFolder.listFiles();
        }
        final int initialFileCount = resultFiles.length;

        SimpleDialog.showProcess(MainWindow, "Extracting files...", new Task()
        {
            @Override
            protected Object call() throws Exception
            {
                Thread rarThread = new Thread(() ->
                {
                    try
                    {
                        String rarPath = rarFile.getCanonicalPath();                        
                        Unrar.extractRARForOS(rarPath, StringHelper.createPlatformPath(ArchiveTempFolder));                        
                    }
                    catch (IOException ex)
                    {
                        appError("Error extracting RAR", ex, true, MainUI);
                    }
                }, "RAR-Extraction-Thread");
                
                rarThread.start();
                while (rarThread.isAlive())
                {
                    File resultFiles[] = tempFolder.listFiles();
                    if (resultFiles != null)
                    {
                        this.updateProgress(resultFiles.length - initialFileCount, 1536);
                    }
                }
                return null;
            }
        });

        ArchiveDataImporter.loadRar(rarFile);
    }
    
	// Adds the set of application icons to the specified stage.
    public static void addApplicationIcons(Stage stage)
    {
        stage.getIcons().addAll(Hawkeye.getGraphic("icons/icon_16.png"),
                                Hawkeye.getGraphic("icons/icon_24.png"),
                                Hawkeye.getGraphic("icons/icon_32.png"),
                                Hawkeye.getGraphic("icons/icon_48.png"),
                                Hawkeye.getGraphic("icons/icon_64.png"),
                                Hawkeye.getGraphic("icons/icon_100.png"),
                                Hawkeye.getGraphic("icons/icon_128.png"),
                                Hawkeye.getGraphic("icons/icon_256.png"));
    }
    
	// Returns the hex representation of the specified color object.
    public static String hexColor(Color color)
    {
        return "\"" + String.format("#%02x%02x%02x", (int)(color.getRed()*255.0), (int)(color.getGreen()*255.0), (int)(color.getBlue()*255.0)) + "\"";
    }
}

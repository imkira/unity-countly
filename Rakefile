#!/usr/bin/env ruby
# -*- coding: UTF-8 -*-

task :default => [:package]

# main config
unity = File.expand_path(ENV['UNITY'] || '/Applications/Unity/Unity.app')
cur_dir = File.expand_path(File.dirname(__FILE__))
proj_path = File.join(cur_dir, 'unity')

# android config
android_jar_name = 'Countly.jar'
android_dir = File.join(cur_dir, 'android')
android_classes = File.join(unity, 'Contents/PlaybackEngines/AndroidPlayer/bin/classes.jar')
android_jar = File.join(android_dir, "bin/#{android_jar_name}")

# package config
package_dir = File.join(cur_dir, 'packages')
package_core_dir = File.join(package_dir, 'core')
package_core = File.join(package_dir, 'unity-countly.unitypackage')
package_demo = File.join(package_dir, 'unity-countly-demo.unitypackage')

namespace :build do
  directory File.join(android_dir, 'libs')

  file android_jar => File.join(android_dir, 'libs') do
    Dir.chdir(android_dir) do
      sh 'android update project -p .'
      cp_r(android_classes, 'libs')
      sh 'ant release'
      mv('bin/classes.jar', "bin/#{android_jar_name}")
    end
  end

  desc 'Build plugin for Android'
  task :android => android_jar do
  end

  desc 'Clean any builds'
  task :clean do
    Dir.chdir(android_dir) do
      sh 'android update project -p .'
      sh 'ant clean'
      rm_rf 'libs'
    end
  end
end

namespace :package do
  def unity_batch(unity, proj_path, method)
    unity_bin = File.join(unity, 'Contents', 'MacOS', 'Unity')
    sh %Q["#{unity_bin}" -projectPath "#{proj_path}" -batchmode -quit -executeMethod "#{method}"]
  end

  directory package_core_dir => package_dir do
    cp_r(proj_path, package_core_dir)
    mkdir_p(File.join(package_core_dir, 'Assets/Plugins/Android'))
  end

  file package_core => [package_core_dir, android_jar] do
    cp_r(android_jar, File.join(package_core_dir, 'Assets/Plugins/Android'))
    unity_batch(unity, package_core_dir, 'PluginBuilder.PackageCore')
    mv(File.join(package_core_dir, File.basename(package_core)), package_core)
  end

  desc 'Create plugin package'
  task :core => package_core do
  end

  file package_demo => [package_core_dir, android_jar] do
    cp_r(android_jar, File.join(package_core_dir, 'Assets/Plugins/Android'))
    unity_batch(unity, package_core_dir, 'PluginBuilder.PackageDemo')
    mv(File.join(package_core_dir, File.basename(package_demo)), package_demo)
  end

  desc 'Create demo package'
  task :demo => package_demo do
  end

  desc 'Clean any packages'
  task :clean do
    rm_rf package_dir
  end
end

desc 'Export plugin and compatibility packages'
task :package => ['package:core', 'package:demo']

desc 'Build plugin for Android'
task :build => ['build:android']

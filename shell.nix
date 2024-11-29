{ pkgs ? import <nixpkgs> {
   config={
      allowUnfree=true;
      cudaSupport=true;
      packageOverrides = pkgs: {
        unstable = import (fetchTarball "https://github.com/NixOS/nixpkgs/archive/staging-next.tar.gz") {
          config.allowUnfree = true;
        };
      };
   };
} }:
pkgs.mkShell {
  buildInputs = [
    pkgs.dotnet-sdk
    pkgs.avalonia-ilspy
  ];
  shellHook = ''
  echo "Honk"
  '';
}